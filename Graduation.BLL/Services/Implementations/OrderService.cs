using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.DTOs;
using Shared.DTOs.Coupon;
using Shared.DTOs.Order;
using Shared.DTOs.Payment;
using Shared.Errors;

namespace Graduation.BLL.Services.Implementations
{
    public class OrderService : IOrderService
    {
        private readonly DatabaseContext _context;
        private readonly IEmailService _emailService;
        private readonly IBackgroundJobClient _backgroundJobs;
        private readonly ILogger<OrderService> _logger;
        private readonly IPaymentService _paymentService;
        private readonly ICouponService _couponService;

        public OrderService(
            DatabaseContext context,
            IEmailService emailService,
            IBackgroundJobClient backgroundJobs,
            ILogger<OrderService> logger,
            IPaymentService paymentService,
            ICouponService couponService)
        {
            _context = context;
            _emailService = emailService;
            _backgroundJobs = backgroundJobs;
            _logger = logger;
            _paymentService = paymentService;
            _couponService = couponService;
        }

        public async Task<CreateOrderResultDto> CreateOrderAsync(string userId, CreateOrderDto dto)
        {
            var clientType = string.IsNullOrWhiteSpace(dto.ClientType)
                ? "web"
                : dto.ClientType.ToLower().Trim();

            if (clientType != "web" && clientType != "mobile")
                clientType = "web";

            string? resolvedAddress = null;
            double? resolvedLat = null;
            double? resolvedLng = null;

            if (dto.AddressId.HasValue)
            {
                var savedAddress = await _context.UserAddresses
                    .FirstOrDefaultAsync(a => a.Id == dto.AddressId.Value && a.UserId == userId);

                if (savedAddress == null)
                    throw new BadRequestException("The selected address was not found.");

                resolvedAddress = savedAddress.FullAddress;
                resolvedLat = savedAddress.Latitude;
                resolvedLng = savedAddress.Longitude;
            }
            else
            {
                if (dto.Latitude == null || dto.Longitude == null)
                    throw new BadRequestException(
                        "Please provide either a saved address ID or GPS coordinates.");

                resolvedAddress = dto.ShippingAddress;
                resolvedLat = dto.Latitude;
                resolvedLng = dto.Longitude;
            }

            var strategy = _context.Database.CreateExecutionStrategy();

            List<CartItem> cartItems = null!;
            List<Order> createdOrders = null!;

            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable);

                try
                {
                    cartItems = await _context.CartItems
                        .Include(ci => ci.Product)
                            .ThenInclude(p => p.Vendor)
                        .Include(ci => ci.SelectedVariants)
                            .ThenInclude(sv => sv.ProductVariant)
                        .Where(ci => ci.UserId == userId)
                        .ToListAsync();

                    if (!cartItems.Any())
                        throw new BadRequestException("Your cart is empty");

                    var invalidItems = cartItems
                        .Where(ci => !ci.Product.Vendor.IsApproved || !ci.Product.Vendor.IsActive)
                        .Select(ci => ci.Product.Vendor.StoreName)
                        .Distinct()
                        .ToList();

                    if (invalidItems.Any())
                        throw new BadRequestException(
                            $"The following vendors are currently unavailable: {string.Join(", ", invalidItems)}. " +
                            "Please remove their products from your cart before placing an order.");

                    ApplyCouponResultDto? couponResult = null;
                    if (!string.IsNullOrWhiteSpace(dto.CouponCode))
                    {
                        var totalSub = cartItems.Sum(i =>
                        {
                            var basePrice = i.Product.DiscountPrice ?? i.Product.Price;
                            var adj = i.SelectedVariants?.Sum(sv => sv.ProductVariant.PriceAdjustment) ?? 0m;
                            return (basePrice + adj) * i.Quantity;
                        });
                        couponResult = await _couponService.ValidateAndCalculateAsync(dto.CouponCode, totalSub);
                    }

                    var vendorGroups = cartItems.GroupBy(ci => ci.Product.VendorId).ToList();
                    createdOrders = new List<Order>();
                    var allOrderItems = new List<OrderItem>();

                    var groupSubtotals = vendorGroups.ToDictionary(
                        g => g.Key,
                        g => g.Sum(i =>
                        {
                            var bp = i.Product.DiscountPrice ?? i.Product.Price;
                            var ad = i.SelectedVariants?.Sum(sv => sv.ProductVariant.PriceAdjustment) ?? 0m;
                            return (bp + ad) * i.Quantity;
                        }));

                    foreach (var vendorGroup in vendorGroups)
                    {
                        var items = vendorGroup.ToList();

                        foreach (var item in items)
                        {
                            if (item.SelectedVariants != null && item.SelectedVariants.Any())
                            {
                                foreach (var sv in item.SelectedVariants)
                                {
                                    int rowsAffected = await _context.Database.ExecuteSqlInterpolatedAsync($@"
                                        UPDATE ProductVariants
                                        SET StockQuantity = StockQuantity - {item.Quantity}
                                        WHERE Id = {sv.ProductVariantId}
                                          AND StockQuantity >= {item.Quantity}
                                          AND IsActive = 1");

                                    if (rowsAffected == 0)
                                        throw new BadRequestException(
                                            $"Product '{item.Product.NameEn}' with variant " +
                                            $"'{sv.ProductVariant.TypeName}: {sv.ProductVariant.Value}' has insufficient stock.");
                                }
                            }
                            else
                            {
                                int rowsAffected = await _context.Database.ExecuteSqlInterpolatedAsync($@"
                                    UPDATE Products
                                    SET StockQuantity = StockQuantity - {item.Quantity}
                                    WHERE Id = {item.ProductId}
                                      AND StockQuantity >= {item.Quantity}
                                      AND IsActive = 1");

                                if (rowsAffected == 0)
                                {
                                    var product = await _context.Products
                                        .AsNoTracking()
                                        .FirstOrDefaultAsync(p => p.Id == item.ProductId);

                                    if (product == null)
                                        throw new NotFoundException($"Product with ID {item.ProductId} not found");
                                    if (!product.IsActive)
                                        throw new BadRequestException($"Product '{product.NameEn}' is no longer available");

                                    throw new BadRequestException($"Product '{product.NameEn}' has insufficient stock.");
                                }
                            }
                        }

                        var subTotal = items.Sum(i =>
                        {
                            var basePrice = i.Product.DiscountPrice ?? i.Product.Price;
                            var adjustment = i.SelectedVariants?.Sum(sv => sv.ProductVariant.PriceAdjustment) ?? 0m;
                            return (basePrice + adjustment) * i.Quantity;
                        });

                        const decimal shippingCost = 50m;
                        var totalCombinedSubtotal = groupSubtotals.Values.Sum();
                        var discountAmount = 0m;
                        if (couponResult != null && totalCombinedSubtotal > 0)
                        {
                            discountAmount = Math.Round(couponResult.DiscountAmount * (subTotal / totalCombinedSubtotal), 2);
                        }
                        var totalAmount = subTotal + shippingCost - discountAmount;

                        var order = new Order
                        {
                            OrderNumber = GenerateOrderNumber(),
                            UserId = userId,
                            SubTotal = subTotal,
                            ShippingCost = shippingCost,
                            DiscountAmount = discountAmount,
                            TotalAmount = totalAmount,
                            Status = OrderStatus.Pending,
                            CouponId = couponResult != null && !createdOrders.Any() ? (await _context.Coupons.Where(c => c.Code == couponResult.Code).Select(c => (int?)c.Id).FirstOrDefaultAsync()) : null,
                            PaymentMethod = dto.PaymentMethod,
                            PaymentStatus = PaymentStatus.Pending,
                            OrderDate = DateTime.UtcNow,
                            ShippingFirstName = dto.ShippingFirstName,
                            ShippingLastName = dto.ShippingLastName,
                            ShippingPhone = dto.ShippingPhone,
                            ShippingAddress = resolvedAddress,
                            ShippingLatitude = resolvedLat,
                            ShippingLongitude = resolvedLng,
                            ClientType = clientType,
                            Notes = dto.Notes
                        };

                        _context.Orders.Add(order);
                        createdOrders.Add(order);

                        foreach (var cartItem in items)
                        {
                            var basePrice = cartItem.Product.DiscountPrice ?? cartItem.Product.Price;
                            var adjustment = cartItem.SelectedVariants?.Sum(sv => sv.ProductVariant.PriceAdjustment) ?? 0m;
                            var unitPrice = basePrice + adjustment;

                            allOrderItems.Add(new OrderItem
                            {
                                Order = order,
                                ProductId = cartItem.ProductId,
                                Quantity = cartItem.Quantity,
                                UnitPrice = unitPrice,
                                TotalPrice = unitPrice * cartItem.Quantity,
                                SelectedVariants = cartItem.SelectedVariants?.Select(sv => new OrderItemVariant
                                {
                                    ProductVariantId = sv.ProductVariantId
                                }).ToList() ?? new List<OrderItemVariant>()
                            });
                        }
                    }

                    _context.OrderItems.AddRange(allOrderItems);

                    _context.CartItems.RemoveRange(cartItems);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    if (couponResult != null)
                    {
                        var coupon = await _context.Coupons.FirstAsync(c => c.Code == couponResult.Code);
                        coupon.CurrentUsageCount++;
                        await _context.SaveChangesAsync();
                    }

                    _logger.LogInformation(
                        "Order(s) created: {OrderNumbers} for user {UserId}",
                        string.Join(", ", createdOrders.Select(o => o.OrderNumber)), userId);
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });

            if (dto.PaymentMethod == PaymentMethod.CashOnDelivery)
            {
                var user = await _context.Users.FindAsync(userId);
                if (user?.Email != null)
                {
                    var first = createdOrders.First();
                    _backgroundJobs.Enqueue<IEmailService>(s =>
                        s.SendOrderConfirmationEmailAsync(user.Email, first.OrderNumber, first.TotalAmount));
                }
            }

            PaymentDto? paymentInfo = null;

            if (dto.PaymentMethod != PaymentMethod.CashOnDelivery)
            {
                foreach (var o in createdOrders)
                {
                    try
                    {
                        await _paymentService.InitiatePaymentAsync(o.Id, o.ClientType);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Failed to initiate Paymob payment for order {OrderNumber}",
                            o.OrderNumber);
                    }
                }

                try
                {
                    paymentInfo = await _paymentService
                        .GetByOrderNumberAsync(createdOrders.First().OrderNumber, userId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to retrieve payment info after initiation");
                }
            }

            var orderDtos = new List<OrderDto>();
            foreach (var o in createdOrders)
                orderDtos.Add(await GetOrderByIdInternalAsync(o.Id));

            return new CreateOrderResultDto
            {
                Orders = orderDtos,
                TotalOrdersCreated = orderDtos.Count,
                Payment = paymentInfo
            };
        }

        public async Task<OrderDto> GetOrderByIdAsync(int id, string userId)
        {
            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.Vendor)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.Images)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.SelectedVariants)
                        .ThenInclude(sv => sv.ProductVariant)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null)
                throw new NotFoundException("Order", id);

            return MapToDto(order);
        }

        public async Task<PagedResult<OrderListDto>> GetUserOrdersAsync(string userId, int pageNumber = 1, int pageSize = 10)
        {

            var query = _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.Vendor)
                .Where(o => o.UserId == userId);

            var totalCount = await query.CountAsync();

            var orders = await query
                .OrderByDescending(o => o.OrderDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<OrderListDto>
            {
                Items = orders.Select(o => MapToListDto(o, null)).ToList(),
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<List<OrderListDto>> GetVendorOrdersAsync(int vendorId)
        {
            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.Vendor)
                .Where(o => o.OrderItems.Any(oi => oi.Product.VendorId == vendorId))
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return orders.Select(o => MapToListDto(o, vendorId)).ToList();
        }

        public async Task<OrderDto> UpdateOrderStatusAsync(int id, int vendorId, UpdateOrderStatusDto dto)
        {
            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.Images)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.SelectedVariants)
                        .ThenInclude(sv => sv.ProductVariant)
                .FirstOrDefaultAsync(o =>
                    o.Id == id && o.OrderItems.Any(oi => oi.Product.VendorId == vendorId));

            if (order == null)
                throw new NotFoundException("Order", id);

            order.Status = dto.Status;

            switch (dto.Status)
            {
                case OrderStatus.Confirmed:
                    order.ConfirmedAt = DateTime.UtcNow;
                    break;
                case OrderStatus.Shipped:
                    order.ShippedAt = DateTime.UtcNow;
                    break;
                case OrderStatus.Delivered:
                    order.DeliveredAt = DateTime.UtcNow;
                    order.PaymentStatus = PaymentStatus.Paid;
                    break;
                case OrderStatus.Cancelled:
                    order.CancelledAt = DateTime.UtcNow;
                    order.CancellationReason = dto.CancellationReason;
                    await RestoreStockAsync(order.OrderItems);
                    break;
            }

            await _context.SaveChangesAsync();
            return MapToDto(order);
        }

        public async Task<OrderDto> AdminUpdateOrderStatusAsync(int id, UpdateOrderStatusDto dto)
        {
            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.Images)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.SelectedVariants)
                        .ThenInclude(sv => sv.ProductVariant)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
                throw new NotFoundException("Order", id);

            order.Status = dto.Status;

            switch (dto.Status)
            {
                case OrderStatus.Confirmed:
                    order.ConfirmedAt = DateTime.UtcNow;
                    break;
                case OrderStatus.Shipped:
                    order.ShippedAt = DateTime.UtcNow;
                    break;
                case OrderStatus.Delivered:
                    order.DeliveredAt = DateTime.UtcNow;
                    order.PaymentStatus = PaymentStatus.Paid;
                    break;
                case OrderStatus.Cancelled:
                    order.CancelledAt = DateTime.UtcNow;
                    order.CancellationReason = dto.CancellationReason;
                    await RestoreStockAsync(order.OrderItems);
                    break;
            }

            await _context.SaveChangesAsync();
            return MapToDto(order);
        }

        public async Task<OrderDto> CancelOrderAsync(int id, string userId, string reason)
        {
            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.Images)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.SelectedVariants)
                        .ThenInclude(sv => sv.ProductVariant)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null)
                throw new NotFoundException("Order", id);

            if (order.Status == OrderStatus.Cancelled)
                throw new BadRequestException("This order has already been cancelled.");

            if (order.Status != OrderStatus.Pending && order.Status != OrderStatus.Confirmed)
                throw new BadRequestException("Can only cancel pending or confirmed orders.");

            order.Status = OrderStatus.Cancelled;
            order.CancelledAt = DateTime.UtcNow;
            order.CancellationReason = reason;

            await RestoreStockAsync(order.OrderItems);
            await _context.SaveChangesAsync();
            return MapToDto(order);
        }

        private async Task RestoreStockAsync(IEnumerable<OrderItem> items)
        {
            foreach (var item in items)
            {
                if (item.SelectedVariants != null && item.SelectedVariants.Any())
                {
                    foreach (var sv in item.SelectedVariants)
                    {
                        await _context.Database.ExecuteSqlInterpolatedAsync($@"
                            UPDATE ProductVariants
                            SET StockQuantity = StockQuantity + {item.Quantity}
                            WHERE Id = {sv.ProductVariantId}");
                    }
                }
                else
                {
                    await _context.Database.ExecuteSqlInterpolatedAsync($@"
                        UPDATE Products
                        SET StockQuantity = StockQuantity + {item.Quantity}
                        WHERE Id = {item.ProductId}");
                }
            }
        }
        public async Task HandleUserAccountDeletionAsync(string userId)
        {
            var activeOrders = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.SelectedVariants)
                .Where(o => o.UserId == userId &&
                            o.Status != OrderStatus.Cancelled &&
                            o.Status != OrderStatus.Delivered)
                .ToListAsync();

            foreach (var order in activeOrders)
                await RestoreStockAsync(order.OrderItems);

            var allOrders = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.SelectedVariants)
                .Where(o => o.UserId == userId)
                .ToListAsync();

            foreach (var order in allOrders)
            {
                foreach (var item in order.OrderItems)
                {
                    if (item.SelectedVariants != null && item.SelectedVariants.Any())
                        _context.Set<OrderItemVariant>().RemoveRange(item.SelectedVariants);
                }
                _context.OrderItems.RemoveRange(order.OrderItems);
            }

            _context.Orders.RemoveRange(allOrders);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "All {Count} order(s) deleted for user {UserId} before account deletion",
                allOrders.Count, userId);
        }
        public async Task<OrderMapTrackingDto> GetOrderMapTrackingAsync(string orderNumber, string userId)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.Vendor)
                .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber && o.UserId == userId);

            if (order == null) throw new NotFoundException("Order not found");

            var vendor = order.OrderItems.FirstOrDefault()?.Product.Vendor;

            return new OrderMapTrackingDto
            {
                OrderNumber = order.OrderNumber,
                Status = order.Status.ToString(),

                DeliveryLatitude = order.ShippingLatitude,
                DeliveryLongitude = order.ShippingLongitude,

                VendorLatitude = vendor?.Latitude,
                VendorLongitude = vendor?.Longitude,

                CurrentLatitude = order.CurrentLatitude ?? vendor?.Latitude,
                CurrentLongitude = order.CurrentLongitude ?? vendor?.Longitude
            };
        }

        private static string GenerateOrderNumber()
            => $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";

        private OrderDto MapToDto(Order order) => new()
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            SubTotal = order.SubTotal,
            ShippingCost = order.ShippingCost,
            DiscountAmount = order.DiscountAmount,
            CouponCode = order.Coupon?.Code,
            TotalAmount = order.TotalAmount,
            Status = order.Status.ToString(),
            PaymentMethod = order.PaymentMethod.ToString(),
            PaymentStatus = order.PaymentStatus.ToString(),
            OrderDate = order.OrderDate,
            DeliveredAt = order.DeliveredAt,
            ShippingFirstName = order.ShippingFirstName,
            ShippingLastName = order.ShippingLastName,
            ShippingPhone = order.ShippingPhone,
            ShippingAddress = order.ShippingAddress,
            ShippingLatitude = order.ShippingLatitude,
            ShippingLongitude = order.ShippingLongitude,
            Notes = order.Notes,
            VendorId = order.OrderItems.FirstOrDefault()?.Product.VendorId ?? 0,
            VendorName = order.OrderItems.FirstOrDefault()?.Product.Vendor?.StoreName ?? "Unknown",
            CurrentLatitude = order.CurrentLatitude,
            CurrentLongitude = order.CurrentLongitude ,
            Items = order.OrderItems.Select(oi => new OrderItemDto
            {
                Id = oi.Id,
                ProductId = oi.ProductId,
                ProductNameAr = oi.Product.NameAr,
                ProductNameEn = oi.Product.NameEn,
                ProductImage = oi.Product.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
                               ?? oi.Product.Images.FirstOrDefault()?.ImageUrl,
                Quantity = oi.Quantity,
                UnitPrice = oi.UnitPrice,
                TotalPrice = oi.TotalPrice,
                VariantTypeName = string.Join(", ", oi.SelectedVariants.Select(sv => sv.ProductVariant.TypeName)),
                VariantValue = string.Join(", ", oi.SelectedVariants.Select(sv => sv.ProductVariant.Value)),
                VariantColorHex = string.Join(", ", oi.SelectedVariants
                    .Select(sv => sv.ProductVariant.ColorHex)
                    .Where(c => !string.IsNullOrEmpty(c)))
            }).ToList()
        };

        private OrderListDto MapToListDto(Order order, int? vendorId = null)
        {
            var vendorItems = vendorId.HasValue
                ? order.OrderItems.Where(oi => oi.Product.VendorId == vendorId.Value).ToList()
                : order.OrderItems.ToList();

            return new OrderListDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                TotalAmount = order.TotalAmount,
                Status = order.Status.ToString(),
                StatusId = (int)order.Status,
                OrderDate = order.OrderDate,
                ItemsCount = vendorItems.Count,
                VendorName = vendorItems.FirstOrDefault()?.Product.Vendor?.StoreName ?? "Unknown",

            };
        }

        private async Task<OrderDto> GetOrderByIdInternalAsync(int id)
        {
            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.Vendor)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.Images)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.SelectedVariants)
                        .ThenInclude(sv => sv.ProductVariant)
                .Include(o => o.Coupon)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
                throw new NotFoundException("Order", id);

            return MapToDto(order);
        }
    }
}