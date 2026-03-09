using Shared.Errors;
using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.DTOs.Order;

namespace Graduation.BLL.Services.Implementations
{
    public class OrderService : IOrderService
    {
        private readonly DatabaseContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<OrderService> _logger;

        public OrderService(
            DatabaseContext context,
            IEmailService emailService,
            ILogger<OrderService> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<CreateOrderResultDto> CreateOrderAsync(string userId, CreateOrderDto dto)
        {
            // FIX #4: Use a single Serializable transaction and defer ALL SaveChangesAsync calls
            // to after the full loop so partial-commit state can never leak out of the transaction.
            using var transaction = await _context.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable);

            try
            {
                var cartItems = await _context.CartItems
                    .Include(ci => ci.Product)
                        .ThenInclude(p => p.Vendor)
                    .Where(ci => ci.UserId == userId)
                    .ToListAsync();

                if (!cartItems.Any())
                    throw new BadRequestException("Your cart is empty");

                // FIX #13 (CartService equivalent): Reject cart items whose vendor is
                // suspended or unapproved before creating the order.
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
                var allCartItemsToRemove = new List<CartItem>(cartItems);

                foreach (var vendorGroup in vendorGroups)
                {
                    var items = vendorGroup.ToList();

                    // Atomic stock decrement per item — participates in the ambient transaction.
                    foreach (var item in items)
                    {
                        var rowsAffected = await _context.Database.ExecuteSqlInterpolatedAsync($@"
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

                            throw new BadRequestException(
                                $"Product '{product.NameEn}' has insufficient stock. " +
                                $"Only {product.StockQuantity} available");
                        }
                    }

                    var subTotal = items.Sum(i => (i.Product.DiscountPrice ?? i.Product.Price) * i.Quantity);
                    var shippingCost = CalculateShipping(dto.ShippingGovernorate);
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
                        ShippingAddress = dto.ShippingAddress,
                        ShippingCity = dto.ShippingCity,
                        ShippingGovernorate = dto.ShippingGovernorate,
                        ShippingPhone = dto.ShippingPhone,
                        Notes = dto.Notes
                    };

                    _context.Orders.Add(order);
                    createdOrders.Add(order);

                    foreach (var cartItem in items)
                    {
                        var unitPrice = cartItem.Product.DiscountPrice ?? cartItem.Product.Price;
                        allOrderItems.Add(new OrderItem
                        {
                            Order = order,   // navigation property — EF resolves Id after SaveChanges
                            ProductId = cartItem.ProductId,
                            Quantity = cartItem.Quantity,
                            UnitPrice = unitPrice,
                            TotalPrice = unitPrice * cartItem.Quantity
                        });
                    }
                }

                // FIX #4: Single SaveChangesAsync for all orders + items + cart removal.
                _context.OrderItems.AddRange(allOrderItems);
                _context.CartItems.RemoveRange(allCartItemsToRemove);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                _logger.LogInformation(
                    "Order(s) created: {OrderNumbers} for user {UserId}",
                    string.Join(", ", createdOrders.Select(o => o.OrderNumber)), userId);

                // Fire-and-forget confirmation email AFTER commit
                var user = await _context.Users.FindAsync(userId);
                if (user?.Email != null)
                {
                    var firstOrder = createdOrders.First();
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _emailService.SendOrderConfirmationEmailAsync(
                                user.Email, firstOrder.OrderNumber, firstOrder.TotalAmount);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to send order confirmation email");
                        }
                    });
                }

                var orderDtos = new List<OrderDto>();
                foreach (var o in createdOrders)
                    orderDtos.Add(await GetOrderByIdInternalAsync(o.Id));

                return new CreateOrderResultDto
                {
                    Orders = orderDtos,
                    TotalOrdersCreated = orderDtos.Count
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
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null)
                throw new NotFoundException("Order", id);

            return MapToDto(order);
        }

        public async Task<List<OrderListDto>> GetUserOrdersAsync(string userId)
        {
            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.Vendor)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return orders.Select(o => MapToListDto(o, null)).ToList();
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
                .FirstOrDefaultAsync(o => o.Id == id && o.OrderItems.Any(oi => oi.Product.VendorId == vendorId));

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
                    foreach (var item in order.OrderItems)
                    {
                        await _context.Database.ExecuteSqlInterpolatedAsync($@"
                            UPDATE Products SET StockQuantity = StockQuantity + {item.Quantity}
                            WHERE Id = {item.ProductId}");
                    }
                    break;
            }

            await _context.SaveChangesAsync();
            return MapToDto(order);
        }

        /// <summary>
        /// FIX #6: Guard against double-cancellation so stock is never restored twice.
        /// Only Pending or Confirmed orders may be cancelled.
        /// </summary>
        public async Task<OrderDto> CancelOrderAsync(int id, string userId, string reason)
        {
            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.Images)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null)
                throw new NotFoundException("Order", id);

            // FIX #6: Reject already-cancelled orders before touching stock.
            if (order.Status == OrderStatus.Cancelled)
                throw new BadRequestException("This order has already been cancelled.");

            if (order.Status != OrderStatus.Pending && order.Status != OrderStatus.Confirmed)
                throw new BadRequestException("Can only cancel pending or confirmed orders.");

            order.Status = OrderStatus.Cancelled;
            order.CancelledAt = DateTime.UtcNow;
            order.CancellationReason = reason;

            foreach (var item in order.OrderItems)
            {
                await _context.Database.ExecuteSqlInterpolatedAsync($@"
                    UPDATE Products SET StockQuantity = StockQuantity + {item.Quantity}
                    WHERE Id = {item.ProductId}");
            }

            await _context.SaveChangesAsync();
            return MapToDto(order);
        }

        // ── Private helpers ────────────────────────────────────────────────────────

        private string GenerateOrderNumber()
            => $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";

        private decimal CalculateShipping(EgyptianGovernorate governorate)
            => governorate switch
            {
                EgyptianGovernorate.Cairo => 30m,
                EgyptianGovernorate.Giza => 30m,
                EgyptianGovernorate.Alexandria => 40m,
                EgyptianGovernorate.Dakahlia => 45m,
                EgyptianGovernorate.Gharbia => 45m,
                EgyptianGovernorate.Sharkia => 45m,
                EgyptianGovernorate.RedSea => 70m,
                EgyptianGovernorate.SouthSinai => 80m,
                EgyptianGovernorate.Aswan => 75m,
                EgyptianGovernorate.Luxor => 70m,
                _ => 50m
            };

        private OrderDto MapToDto(Order order) => new()
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            SubTotal = order.SubTotal,
            ShippingCost = order.ShippingCost,
            TotalAmount = order.TotalAmount,
            Status = order.Status.ToString(),
            StatusId = (int)order.Status,
            PaymentMethod = order.PaymentMethod.ToString(),
            PaymentStatus = order.PaymentStatus.ToString(),
            OrderDate = order.OrderDate,
            DeliveredAt = order.DeliveredAt,
            ShippingFirstName = order.ShippingFirstName,
            ShippingLastName = order.ShippingLastName,
            ShippingAddress = order.ShippingAddress,
            ShippingCity = order.ShippingCity,
            ShippingGovernorate = order.ShippingGovernorate.ToString(),
            ShippingPhone = order.ShippingPhone,
            Notes = order.Notes,
            VendorId = order.OrderItems.FirstOrDefault()?.Product.VendorId ?? 0,
            VendorName = order.OrderItems.FirstOrDefault()?.Product.Vendor?.StoreName ?? "Unknown",
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
                TotalPrice = oi.TotalPrice
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
                VendorName = vendorItems.FirstOrDefault()?.Product.Vendor?.StoreName ?? "Unknown"
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
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
                throw new NotFoundException("Order", id);

            return MapToDto(order);
        }
    }
}
