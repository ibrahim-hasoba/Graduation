using Graduation.API.Errors;
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

        public async Task<OrderDto> CreateOrderAsync(string userId, CreateOrderDto dto)
        {
            // CRITICAL FIX: Added transaction for atomicity with proper isolation level
            using var transaction = await _context.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable);

            try
            {
                // Get user's cart
                var cartItems = await _context.CartItems
                    .Include(ci => ci.Product)
                        .ThenInclude(p => p.Vendor)
                    .Where(ci => ci.UserId == userId)
                    .ToListAsync();

                if (!cartItems.Any())
                    throw new BadRequestException("Your cart is empty");

                // Group by vendor (each vendor gets separate order)
                var vendorGroups = cartItems.GroupBy(ci => ci.Product.VendorId);

                var createdOrders = new List<Order>();

                foreach (var vendorGroup in vendorGroups)
                {
                    var vendorId = vendorGroup.Key;
                    var items = vendorGroup.ToList();

                    // IMPROVED FIX: Use atomic SQL updates to prevent race conditions
                    foreach (var item in items)
                    {
                        // Atomic decrement with stock validation in a single SQL statement
                        var rowsAffected = await _context.Database.ExecuteSqlInterpolatedAsync($@"
                            UPDATE Products 
                            SET StockQuantity = StockQuantity - {item.Quantity}
                            WHERE Id = {item.ProductId} 
                            AND StockQuantity >= {item.Quantity}
                            AND IsActive = 1");

                        if (rowsAffected == 0)
                        {
                            // Check why the update failed
                            var product = await _context.Products
                                .AsNoTracking()
                                .FirstOrDefaultAsync(p => p.Id == item.ProductId);

                            if (product == null)
                                throw new NotFoundException($"Product with ID {item.ProductId} not found");

                            if (!product.IsActive)
                                throw new BadRequestException($"Product '{product.NameEn}' is no longer available");

                            if (product.StockQuantity < item.Quantity)
                                throw new BadRequestException(
                                    $"Product '{product.NameEn}' has insufficient stock. Only {product.StockQuantity} available");

                            // If we get here, something unexpected happened
                            throw new BadRequestException($"Failed to reserve stock for '{product.NameEn}'");
                        }
                    }

                    // Calculate totals
                    var subTotal = items.Sum(i => (i.Product.DiscountPrice ?? i.Product.Price) * i.Quantity);
                    var shippingCost = CalculateShipping(dto.ShippingGovernorate);
                    var totalAmount = subTotal + shippingCost;

                    // Generate order number
                    var orderNumber = GenerateOrderNumber();

                    // Create order
                    var order = new Order
                    {
                        OrderNumber = orderNumber,
                        UserId = userId,
                        VendorId = vendorId,
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
                    await _context.SaveChangesAsync();

                    // Create order items
                    foreach (var cartItem in items)
                    {
                        var unitPrice = cartItem.Product.DiscountPrice ?? cartItem.Product.Price;

                        var orderItem = new OrderItem
                        {
                            OrderId = order.Id,
                            ProductId = cartItem.ProductId,
                            Quantity = cartItem.Quantity,
                            UnitPrice = unitPrice,
                            TotalPrice = unitPrice * cartItem.Quantity
                        };

                        _context.OrderItems.Add(orderItem);
                    }

                    await _context.SaveChangesAsync();
                    createdOrders.Add(order);

                    // Remove items from cart
                    _context.CartItems.RemoveRange(items);
                }

                await _context.SaveChangesAsync();

                // Commit transaction
                await transaction.CommitAsync();

                _logger.LogInformation("Order created successfully: {OrderNumber} for user {UserId}",
                    createdOrders.First().OrderNumber, userId);

                // Send confirmation email (async, don't await to not block response)
                var user = await _context.Users.FindAsync(userId);
                if (user != null && !string.IsNullOrEmpty(user.Email))
                {
                    var firstOrder = createdOrders.First();
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _emailService.SendOrderConfirmationEmailAsync(
                                user.Email,
                                firstOrder.OrderNumber,
                                firstOrder.TotalAmount);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to send order confirmation email");
                        }
                    });
                }

                // Return first order
                return await GetOrderByIdAsync(createdOrders.First().Id, userId);
            }
            catch (Exception ex)
            {
                // Rollback on any error
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to create order for user {UserId}", userId);
                throw;
            }
        }

        public async Task<OrderDto> GetOrderByIdAsync(int id, string userId)
        {
            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.Vendor)
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
                .Include(o => o.Vendor)
                .Include(o => o.OrderItems)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return orders.Select(MapToListDto).ToList();
        }

        public async Task<List<OrderListDto>> GetVendorOrdersAsync(int vendorId)
        {
            var orders = await _context.Orders
                .Include(o => o.Vendor)
                .Include(o => o.OrderItems)
                .Where(o => o.VendorId == vendorId)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return orders.Select(MapToListDto).ToList();
        }

        public async Task<OrderDto> UpdateOrderStatusAsync(int id, int vendorId, UpdateOrderStatusDto dto)
        {
            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.Vendor)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.Images)
                .FirstOrDefaultAsync(o => o.Id == id && o.VendorId == vendorId);

            if (order == null)
                throw new NotFoundException("Order", id);

            // Validate status transition
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
                    // Restore stock using atomic update
                    foreach (var item in order.OrderItems)
                    {
                        await _context.Database.ExecuteSqlInterpolatedAsync($@"
                            UPDATE Products 
                            SET StockQuantity = StockQuantity + {item.Quantity}
                            WHERE Id = {item.ProductId}");
                    }
                    break;
            }

            await _context.SaveChangesAsync();

            return MapToDto(order);
        }

        public async Task<OrderDto> CancelOrderAsync(int id, string userId, string reason)
        {
            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.Vendor)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.Images)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null)
                throw new NotFoundException("Order", id);

            if (order.Status != OrderStatus.Pending && order.Status != OrderStatus.Confirmed)
                throw new BadRequestException("Can only cancel pending or confirmed orders");

            order.Status = OrderStatus.Cancelled;
            order.CancelledAt = DateTime.UtcNow;
            order.CancellationReason = reason;

            // Restore stock using atomic update
            foreach (var item in order.OrderItems)
            {
                await _context.Database.ExecuteSqlInterpolatedAsync($@"
                    UPDATE Products 
                    SET StockQuantity = StockQuantity + {item.Quantity}
                    WHERE Id = {item.ProductId}");
            }

            await _context.SaveChangesAsync();

            return MapToDto(order);
        }

        private string GenerateOrderNumber()
        {
            return $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper()}";
        }

        private decimal CalculateShipping(EgyptianGovernorate governorate)
        {
            // Egyptian shipping rates by governorate
            return governorate switch
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
                _ => 50m // Default shipping
            };
        }

        private OrderDto MapToDto(Order order)
        {
            return new OrderDto
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
                VendorId = order.VendorId,
                VendorName = order.Vendor.StoreName,
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
        }

        private OrderListDto MapToListDto(Order order)
        {
            return new OrderListDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                TotalAmount = order.TotalAmount,
                Status = order.Status.ToString(),
                StatusId = (int)order.Status,
                OrderDate = order.OrderDate,
                ItemsCount = order.OrderItems.Count,
                VendorName = order.Vendor.StoreName
            };
        }
    }
}
