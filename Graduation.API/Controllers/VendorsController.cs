using Shared.Errors;
using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Graduation.API.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Graduation.API.Controllers
{
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
            var firstDayOf6MonthsAgo = new DateTime(
                today.AddMonths(-5).Year,
                today.AddMonths(-5).Month, 1);

            var totalProducts = await _context.Products
                .CountAsync(p => p.VendorId == vendorId);
            var activeProducts = await _context.Products
                .CountAsync(p => p.VendorId == vendorId && p.IsActive);
            var outOfStockProducts = await _context.Products
                .CountAsync(p => p.VendorId == vendorId && p.IsActive && p.StockQuantity == 0);

            
            var vendorOrderIdsQuery = _context.OrderItems
                .Where(oi => oi.Product.VendorId == vendorId)
                .Select(oi => oi.OrderId)
                .Distinct();

            var orderStatusCounts = await _context.Orders
                .Where(o => vendorOrderIdsQuery.Contains(o.Id))
                .GroupBy(o => o.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            var totalOrders = orderStatusCounts.Sum(x => x.Count);
            var pendingOrders = orderStatusCounts
                .FirstOrDefault(x => x.Status == OrderStatus.Pending)?.Count ?? 0;
            var completedOrders = orderStatusCounts
                .FirstOrDefault(x => x.Status == OrderStatus.Delivered)?.Count ?? 0;
            var cancelledOrders = orderStatusCounts
                .FirstOrDefault(x => x.Status == OrderStatus.Cancelled)?.Count ?? 0;

            var todayOrders = await _context.Orders
                .CountAsync(o => vendorOrderIdsQuery.Contains(o.Id) && o.OrderDate >= today);

            var deliveredOrderIdsQuery = _context.Orders
                .Where(o => vendorOrderIdsQuery.Contains(o.Id) && o.Status == OrderStatus.Delivered)
                .Select(o => o.Id);

            var totalRevenue = await _context.OrderItems
                .Where(oi => oi.Product.VendorId == vendorId
                          && deliveredOrderIdsQuery.Contains(oi.OrderId))
                .SumAsync(oi => (decimal?)oi.TotalPrice) ?? 0m;

            var monthlyDeliveredIdsQuery = _context.Orders
                .Where(o => vendorOrderIdsQuery.Contains(o.Id)
                         && o.Status == OrderStatus.Delivered
                         && o.OrderDate >= firstDayOfMonth)
                .Select(o => o.Id);

            var monthlyRevenue = await _context.OrderItems
                .Where(oi => oi.Product.VendorId == vendorId
                          && monthlyDeliveredIdsQuery.Contains(oi.OrderId))
                .SumAsync(oi => (decimal?)oi.TotalPrice) ?? 0m;

            var reviewStats = await _context.ProductReviews
                .Where(r => r.Product.VendorId == vendorId && r.IsApproved)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Count = g.Count(),
                    Avg = g.Average(r => (double)r.Rating)
                })
                .FirstOrDefaultAsync();

            var averageRating = reviewStats != null ? Math.Round(reviewStats.Avg, 1) : 0.0;
            var totalReviews = reviewStats?.Count ?? 0;

            var monthlyChart = await _context.Orders
                .Where(o => vendorOrderIdsQuery.Contains(o.Id)
                         && o.Status == OrderStatus.Delivered
                         && o.OrderDate >= firstDayOf6MonthsAgo)
                .GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month })
                .Select(g => new
                {
                    label = $"{g.Key.Year}-{g.Key.Month:00}",
                    revenue = Math.Round(
                        _context.OrderItems
                            .Where(oi => oi.Product.VendorId == vendorId
                                      && g.Select(o => o.Id).Contains(oi.OrderId))
                            .Sum(oi => (decimal?)oi.TotalPrice) ?? 0m, 2),
                    orderCount = g.Count()
                })
                .OrderBy(x => x.label)
                .ToListAsync();

            var topProducts = await _context.OrderItems
                .Where(oi => oi.Product.VendorId == vendorId)
                .GroupBy(oi => oi.ProductId)
                .Select(g => new
                {
                    productId = g.Key,
                    totalSold = g.Sum(oi => oi.Quantity),
                    revenue = Math.Round(g.Sum(oi => oi.TotalPrice), 2)
                })
                .OrderByDescending(x => x.revenue)
                .Take(5)
                .ToListAsync();

            return Ok(new Errors.ApiResult(data: new
            {
                vendorId,
                storeName = vendor.StoreName,
                totalProducts,
                activeProducts,
                outOfStockProducts,
                totalOrders,
                pendingOrders,
                completedOrders,
                cancelledOrders,
                todayOrders,
                totalRevenue = Math.Round(totalRevenue, 2),
                monthlyRevenue = Math.Round(monthlyRevenue, 2),
                averageRating,
                totalReviews,
                monthlyRevenueChart = monthlyChart,
                topProducts
            }));
        }

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

            var timeline = new List<object>
            {
                new { step = "Placed", status = "Placed", timestamp = (DateTime?)order.OrderDate, completed = true }
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
