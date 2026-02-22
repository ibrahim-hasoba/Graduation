using Graduation.API.Errors;
using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Graduation.API.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Graduation.API.Controllers
{
    /// <summary>
    /// Vendor-specific dashboard statistics.
    /// Previously vendors had no stats endpoint — they could list their own orders/products
    /// but had no aggregated view of their performance.
    /// </summary>
    [Route("api/vendor")]
    [ApiController]
    [Authorize]
    public class VendorStatsController : ControllerBase
    {
        private readonly IVendorService _vendorService;
        private readonly DatabaseContext _context;

        public VendorStatsController(IVendorService vendorService, DatabaseContext context)
        {
            _vendorService = vendorService;
            _context = context;
        }

        /// <summary>
        /// Get aggregated dashboard stats for the authenticated vendor.
        /// Returns revenue, order counts, product counts, avg rating, and monthly breakdown.
        /// </summary>
        [HttpGet("my-stats")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetMyStats()
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            var vendor = await _vendorService.GetVendorByUserIdAsync(userId);
            if (vendor == null)
                return NotFound(new ApiResponse(404, "You don't have a vendor account"));

            var vendorId = vendor.Id;
            var today = DateTime.UtcNow.Date;
            var firstDayOfMonth = new DateTime(today.Year, today.Month, 1);
            var last30Days = today.AddDays(-29);

            // Product stats
            var totalProducts = await _context.Products
                .CountAsync(p => p.VendorId == vendorId);
            var activeProducts = await _context.Products
                .CountAsync(p => p.VendorId == vendorId && p.IsActive);
            var outOfStockProducts = await _context.Products
                .CountAsync(p => p.VendorId == vendorId && p.IsActive && p.StockQuantity == 0);

            // Order stats
            var orderItems = await _context.OrderItems
                .Include(oi => oi.Order)
                .Where(oi => oi.Product.VendorId == vendorId)
                .ToListAsync();

            var allOrderIds = orderItems.Select(oi => oi.OrderId).Distinct().ToList();
            var orders = await _context.Orders
                .Where(o => allOrderIds.Contains(o.Id))
                .ToListAsync();

            var totalOrders = orders.Count;
            var pendingOrders = orders.Count(o => o.Status == OrderStatus.Pending);
            var completedOrders = orders.Count(o => o.Status == OrderStatus.Delivered);
            var cancelledOrders = orders.Count(o => o.Status == OrderStatus.Cancelled);

            // Revenue
            var deliveredOrders = orders.Where(o => o.Status == OrderStatus.Delivered).ToList();
            var deliveredOrderIds = deliveredOrders.Select(o => o.Id).ToHashSet();
            var totalRevenue = orderItems
                .Where(oi => deliveredOrderIds.Contains(oi.OrderId))
                .Sum(oi => oi.TotalPrice);

            var monthlyOrderIds = orders
                .Where(o => o.Status == OrderStatus.Delivered && o.OrderDate >= firstDayOfMonth)
                .Select(o => o.Id)
                .ToHashSet();
            var monthlyRevenue = orderItems
                .Where(oi => monthlyOrderIds.Contains(oi.OrderId))
                .Sum(oi => oi.TotalPrice);

            var todayOrderIds = orders
                .Where(o => o.OrderDate >= today)
                .Select(o => o.Id)
                .ToHashSet();
            var todayOrders = todayOrderIds.Count;

            // Reviews / rating
            var reviews = await _context.ProductReviews
                .Where(r => r.Product.VendorId == vendorId && r.IsApproved)
                .ToListAsync();

            var averageRating = reviews.Any() ? Math.Round(reviews.Average(r => r.Rating), 1) : 0.0;
            var totalReviews = reviews.Count;

            // Monthly revenue chart (last 6 months)
            var sixMonthsAgo = today.AddMonths(-5);
            var firstDayOf6MonthsAgo = new DateTime(sixMonthsAgo.Year, sixMonthsAgo.Month, 1);

            var monthlyChart = orders
                .Where(o => o.Status == OrderStatus.Delivered && o.OrderDate >= firstDayOf6MonthsAgo)
                .GroupJoin(
                    orderItems,
                    o => o.Id,
                    oi => oi.OrderId,
                    (o, items) => new { o.OrderDate, Revenue = items.Sum(i => i.TotalPrice) })
                .GroupBy(x => new { x.OrderDate.Year, x.OrderDate.Month })
                .Select(g => new
                {
                    label = $"{g.Key.Year}-{g.Key.Month:00}",
                    revenue = Math.Round(g.Sum(x => x.Revenue), 2),
                    orderCount = g.Count()
                })
                .OrderBy(x => x.label)
                .ToList();

            // Top 5 products by revenue
            var topProducts = orderItems
                .GroupBy(oi => oi.ProductId)
                .Select(g => new
                {
                    productId = g.Key,
                    totalSold = g.Sum(oi => oi.Quantity),
                    revenue = Math.Round(g.Sum(oi => oi.TotalPrice), 2)
                })
                .OrderByDescending(x => x.revenue)
                .Take(5)
                .ToList();

            return Ok(new Errors.ApiResult(data: new
            {
                vendorId,
                storeName = vendor.StoreName,

                // Products
                totalProducts,
                activeProducts,
                outOfStockProducts,

                // Orders
                totalOrders,
                pendingOrders,
                completedOrders,
                cancelledOrders,
                todayOrders,

                // Revenue
                totalRevenue = Math.Round(totalRevenue, 2),
                monthlyRevenue = Math.Round(monthlyRevenue, 2),

                // Reviews
                averageRating,
                totalReviews,

                // Charts
                monthlyRevenueChart = monthlyChart,
                topProducts
            }));
        }

        /// <summary>
        /// Get order status timeline for a specific vendor order.
        /// Exposes the ConfirmedAt, ShippedAt, DeliveredAt, CancelledAt timestamps
        /// that exist on the Order entity but were never surfaced in the API response.
        /// </summary>
        [HttpGet("orders/{orderId}/timeline")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetOrderTimeline(int orderId)
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            var vendor = await _vendorService.GetVendorByUserIdAsync(userId);

            // Allow both customers and vendors to fetch the timeline for their own orders
            Order? order;
            if (vendor != null)
            {
                order = await _context.Orders
                    .Include(o => o.OrderItems)
                    .FirstOrDefaultAsync(o =>
                        o.Id == orderId &&
                        o.OrderItems.Any(oi => oi.Product.VendorId == vendor.Id));
            }
            else
            {
                order = await _context.Orders
                    .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);
            }

            if (order == null)
                return NotFound(new ApiResponse(404, "Order not found"));

            // Build timeline steps — only include steps that have actually occurred
            var timeline = new List<object>
            {
                new { step = "Placed",    status = "Placed",    timestamp = (DateTime?)order.OrderDate,    completed = true }
            };

            if (order.ConfirmedAt.HasValue || order.Status >= OrderStatus.Confirmed)
                timeline.Add(new { step = "Confirmed", status = "Confirmed", timestamp = order.ConfirmedAt, completed = order.ConfirmedAt.HasValue });

            if (order.ShippedAt.HasValue || order.Status >= OrderStatus.Shipped)
                timeline.Add(new { step = "Shipped", status = "Shipped", timestamp = order.ShippedAt, completed = order.ShippedAt.HasValue });

            if (order.DeliveredAt.HasValue || order.Status >= OrderStatus.Delivered)
                timeline.Add(new { step = "Delivered", status = "Delivered", timestamp = order.DeliveredAt, completed = order.DeliveredAt.HasValue });

            if (order.CancelledAt.HasValue)
                timeline.Add(new { step = "Cancelled", status = "Cancelled", timestamp = order.CancelledAt, completed = true });

            return Ok(new Errors.ApiResult(data: new
            {
                orderId = order.Id,
                orderNumber = order.OrderNumber,
                currentStatus = order.Status.ToString(),
                timeline
            }));
        }
    }
}
