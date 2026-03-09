using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs.Report;
using Shared.Errors;

namespace Graduation.BLL.Services.Implementations
{
    public class ReportService : IReportService
    {
        private readonly DatabaseContext _context;

        public ReportService(DatabaseContext context)
        {
            _context = context;
        }

        public async Task<SalesReportDto> GetSalesReportAsync(
            DateTime startDate, DateTime endDate, int? vendorId = null)
        {
            var query = _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .Where(o => o.OrderDate >= startDate && o.OrderDate <= endDate)
                .AsQueryable();

            if (vendorId.HasValue)
                query = query.Where(o => o.OrderItems.Any(oi => oi.Product.VendorId == vendorId.Value));

            var orders = await query.ToListAsync();

            // FIX #17: For vendor-scoped reports, only sum that vendor's items — not the full order total
            var totalRevenue = vendorId.HasValue
                ? orders.SelectMany(o => o.OrderItems)
                        .Where(oi => oi.Product.VendorId == vendorId.Value)
                        .Sum(oi => oi.TotalPrice)
                : orders.Sum(o => o.TotalAmount);

            return new SalesReportDto
            {
                StartDate = startDate,
                EndDate = endDate,
                TotalOrders = orders.Count,
                TotalRevenue = decimal.Round(totalRevenue, 2),
                DeliveredOrders = orders.Count(o => o.Status == OrderStatus.Delivered),
                PendingOrders = orders.Count(o => o.Status == OrderStatus.Pending),
                CancelledOrders = orders.Count(o => o.Status == OrderStatus.Cancelled),
                AverageOrderValue = orders.Any() ? decimal.Round(totalRevenue / orders.Count, 2) : 0
            };
        }

        public async Task<List<CategorySalesDto>> GetSalesByCategoryAsync(
            DateTime startDate, DateTime endDate)
        {
            return await _context.OrderItems
                .IgnoreQueryFilters()
                .Include(oi => oi.Product)
                    .ThenInclude(p => p.Category)
                .Where(oi => oi.Order.OrderDate >= startDate
                          && oi.Order.OrderDate <= endDate
                          && oi.Order.Status == OrderStatus.Delivered)
                .GroupBy(oi => new { oi.Product.CategoryId, oi.Product.Category.NameEn })
                .Select(g => new CategorySalesDto
                {
                    CategoryId = g.Key.CategoryId,
                    CategoryName = g.Key.NameEn,
                    TotalSales = g.Sum(oi => oi.TotalPrice),
                    TotalOrders = g.Select(oi => oi.OrderId).Distinct().Count(),
                    TotalItemsSold = g.Sum(oi => oi.Quantity)
                })
                .OrderByDescending(c => c.TotalSales)
                .ToListAsync();
        }

        
        public async Task<VendorPerformanceDto> GetVendorPerformanceAsync(int vendorId)
        {
            var vendor = await _context.Vendors
                .Include(v => v.Products)
                .FirstOrDefaultAsync(v => v.Id == vendorId)
                ?? throw new NotFoundException("Vendor", vendorId);

            var vendorItems = await _context.OrderItems
                .IgnoreQueryFilters()
                .Include(oi => oi.Order)
                .Include(oi => oi.Product)
                    .ThenInclude(p => p.Images)
                .Where(oi => oi.Product.VendorId == vendorId
                          && oi.Order.Status == OrderStatus.Delivered)
                .ToListAsync();

            var totalRevenue = decimal.Round(vendorItems.Sum(oi => oi.TotalPrice), 2);
            var totalOrderIds = vendorItems.Select(oi => oi.OrderId).Distinct().Count();
            var avgOrderValue = totalOrderIds > 0
                ? decimal.Round(totalRevenue / totalOrderIds, 2)
                : 0m;

            var topProducts = vendorItems
                .GroupBy(oi => new
                {
                    oi.ProductId,
                    oi.Product.NameEn,
                    oi.Product.NameAr,
                    oi.Product.Images
                })
                .Select(g => new TopProductDto
                {
                    Id = g.Key.ProductId,
                    NameEn = g.Key.NameEn,
                    NameAr = g.Key.NameAr,
                    ImageUrl = g.Key.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
                            ?? g.Key.Images.FirstOrDefault()?.ImageUrl,
                    TotalSales = g.Sum(oi => oi.Quantity),
                    Revenue = decimal.Round(g.Sum(oi => oi.TotalPrice), 2),
                    VendorName = vendor.StoreName
                })
                .OrderByDescending(p => p.Revenue)
                .Take(10)
                .ToList();

            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            var recentRevenue = decimal.Round(
                vendorItems
                    .Where(oi => oi.Order.DeliveredAt >= thirtyDaysAgo)
                    .Sum(oi => oi.TotalPrice), 2);

            return new VendorPerformanceDto
            {
                VendorId = vendorId,
                StoreName = vendor.StoreName,
                TotalRevenue = totalRevenue,
                TotalOrders = totalOrderIds,
                AverageOrderValue = avgOrderValue,
                TotalProducts = vendor.Products.Count,
                ActiveProducts = vendor.Products.Count(p => p.IsActive),
                TopProducts = topProducts,
                RevenueLastThirtyDays = recentRevenue
            };
        }

        public async Task<CustomerInsightsDto> GetCustomerInsightsAsync()
        {
            var totalCustomers = await _context.Users.CountAsync();
            var activeCustomers = await _context.Orders
                .Select(o => o.UserId)
                .Distinct()
                .CountAsync();

            var repeatCustomers = await _context.Orders
                .GroupBy(o => o.UserId)
                .CountAsync(g => g.Count() > 1);

            return new CustomerInsightsDto
            {
                TotalCustomers = totalCustomers,
                ActiveCustomers = activeCustomers,
                RepeatCustomers = repeatCustomers,
                CustomerRetentionRate = activeCustomers > 0
                    ? Math.Round((double)repeatCustomers / activeCustomers * 100, 1)
                    : 0
            };
        }

        public async Task<List<LowStockProductDto>> GetLowStockProductsAsync(int threshold = 10)
        {
            return await _context.Products
                .Include(p => p.Vendor)
                .Where(p => p.IsActive && p.StockQuantity <= threshold)
                .OrderBy(p => p.StockQuantity)
                .Select(p => new LowStockProductDto
                {
                    ProductId = p.Id,
                    ProductName = p.NameEn,
                    CurrentStock = p.StockQuantity,
                    VendorName = p.Vendor.StoreName,
                    VendorId = p.VendorId
                })
                .ToListAsync();
        }

        public async Task<List<VendorRevenueDto>> GetRevenueByVendorAsync(
            DateTime startDate, DateTime endDate, int take = 10)
        {
            return await _context.OrderItems
                .IgnoreQueryFilters()
                .Include(oi => oi.Product)
                    .ThenInclude(p => p.Vendor)
                .Where(oi => oi.Order.OrderDate >= startDate
                          && oi.Order.OrderDate <= endDate
                          && oi.Order.Status == OrderStatus.Delivered)
                .GroupBy(oi => new { oi.Product.VendorId, oi.Product.Vendor.StoreName })
                .Select(g => new VendorRevenueDto
                {
                    VendorId = g.Key.VendorId,
                    StoreName = g.Key.StoreName,
                    TotalRevenue = decimal.Round(g.Sum(oi => oi.TotalPrice), 2),
                    TotalOrders = g.Select(oi => oi.OrderId).Distinct().Count()
                })
                .OrderByDescending(v => v.TotalRevenue)
                .Take(take)
                .ToListAsync();
        }

        public async Task<List<TopProductDto>> GetTopProductsAsync(
            DateTime startDate, DateTime endDate, int take = 10)
        {
            // Aggregate in DB first, then fetch images in a separate query
            // to avoid EF GroupBy + navigation collection limitations.
            var grouped = await _context.OrderItems
                .IgnoreQueryFilters()
                .Include(oi => oi.Product)
                    .ThenInclude(p => p.Vendor)
                .Where(oi => oi.Order.OrderDate >= startDate
                          && oi.Order.OrderDate <= endDate
                          && oi.Order.Status == OrderStatus.Delivered)
                .GroupBy(oi => new
                {
                    oi.ProductId,
                    oi.Product.NameEn,
                    oi.Product.NameAr,
                    oi.Product.Vendor.StoreName
                })
                .Select(g => new
                {
                    g.Key.ProductId,
                    g.Key.NameEn,
                    g.Key.NameAr,
                    g.Key.StoreName,
                    TotalSales = g.Sum(oi => oi.Quantity),
                    Revenue = decimal.Round(g.Sum(oi => oi.TotalPrice), 2)
                })
                .OrderByDescending(p => p.TotalSales)
                .Take(take)
                .ToListAsync();

            // Fetch primary images for the top products in a single focused query
            var productIds = grouped.Select(g => g.ProductId).ToList();
            var images = await _context.ProductImages
                .Where(i => productIds.Contains(i.ProductId))
                .GroupBy(i => i.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    ImageUrl = g.OrderByDescending(i => i.IsPrimary).First().ImageUrl
                })
                .ToDictionaryAsync(x => x.ProductId, x => x.ImageUrl);

            return grouped.Select(g => new TopProductDto
            {
                Id = g.ProductId,
                NameEn = g.NameEn,
                NameAr = g.NameAr,
                ImageUrl = images.GetValueOrDefault(g.ProductId),
                TotalSales = g.TotalSales,
                Revenue = g.Revenue,
                VendorName = g.StoreName
            }).ToList();
        }

        public async Task<List<OrderStatusSummaryDto>> GetOrderStatusSummaryAsync()
        {
            return await _context.Orders
                .GroupBy(o => o.Status)
                .Select(g => new OrderStatusSummaryDto
                {
                    Status = g.Key.ToString(),
                    Count = g.Count(),
                    TotalAmount = decimal.Round(g.Sum(o => o.TotalAmount), 2)
                })
                .ToListAsync();
        }

        public async Task<List<UserTrendDto>> GetUserTrendsAsync()
        {
            var sixMonthsAgo = DateTime.UtcNow.AddDays(-180);

            return await _context.Users
                .Where(u => u.CreatedAt >= sixMonthsAgo)
                .GroupBy(u => new { u.CreatedAt.Year, u.CreatedAt.Month })
                .Select(g => new UserTrendDto
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    NewUsers = g.Count()
                })
                .OrderBy(t => t.Year)
                .ThenBy(t => t.Month)
                .ToListAsync();
        }
    }
}
