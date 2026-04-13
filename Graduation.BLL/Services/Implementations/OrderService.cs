using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.DTOs;
using Shared.DTOs.Order;
using Shared.DTOs.Payment;
using Shared.Errors;

namespace Graduation.BLL.Services.Implementations
{
    public class OrderService : IOrderService
    {
        private readonly DatabaseContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<OrderService> _logger;
        private readonly IPaymentService _paymentService;

        public OrderService(
            DatabaseContext context,
            IEmailService emailService,
            ILogger<OrderService> logger,
            IPaymentService paymentService)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
            _paymentService = paymentService;
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

            using var transaction = await _context.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable);

            try
            {
                var cartItems = await _context.CartItems
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

                var vendorGroups = cartItems.GroupBy(ci => ci.Product.VendorId).ToList();
                var createdOrders = new List<Order>();
                var allOrderItems = new List<OrderItem>();

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
                    var totalAmount = subTotal + shippingCost;

                    var order = new Order
                    {
                        OrderNumber = GenerateOrderNumber(),
                        UserId = userId,
                        SubTotal = subTotal,
                        ShippingCost = shippingCost,
                        TotalAmount = totalAmount,
                        Status = OrderStatus.Pending,
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

                
                if (dto.PaymentMethod == PaymentMethod.CashOnDelivery)
                    _context.CartItems.RemoveRange(cartItems);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation(
                    "Order(s) created: {OrderNumbers} for user {UserId}",
                    string.Join(", ", createdOrders.Select(o => o.OrderNumber)), userId);

                if (dto.PaymentMethod == PaymentMethod.CashOnDelivery)
                {
                    var user = await _context.Users.FindAsync(userId);
                    if (user?.Email != null)
                    {
                        var first = createdOrders.First();
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await _emailService.SendOrderConfirmationEmailAsync(
                                    user.Email, first.OrderNumber, first.TotalAmount);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to send order confirmation email");
                            }
                        });
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
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
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

            if (order.Status == OrderStatus.Cancelled || order.Status == OrderStatus.Delivered)
                throw new BadRequestException("Cannot update status of cancelled or delivered orders");

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
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
                throw new NotFoundException("Order", id);

            return MapToDto(order);
        }
    }
}
