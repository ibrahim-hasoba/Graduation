using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Graduation.BLL.Services.Implementations
{
  public class ReportService : IReportService
  {
    private readonly DatabaseContext _context;
    private readonly ILogger<ReportService> _logger;

    public ReportService(
        DatabaseContext context,
        ILogger<ReportService> logger)
    {
      _context = context;
      _logger = logger;
    }

    public async Task<dynamic> GetSalesReportAsync(DateTime startDate, DateTime endDate, int? vendorId = null)
    {
      var orders = await _context.Orders
          .Where(o => o.OrderDate >= startDate && o.OrderDate <= endDate)
          .Include(o => o.OrderItems)
              .ThenInclude(oi => oi.Product)
                  .ThenInclude(p => p.Vendor)
          .ToListAsync();

      if (vendorId.HasValue)
      {
        orders = orders.Where(o => o.OrderItems.Any(oi => oi.Product.VendorId == vendorId)).ToList();
      }

      var deliveredOrders = orders.Where(o => o.Status == OrderStatus.Delivered).ToList();

      var totalSales = deliveredOrders.Sum(o => o.TotalAmount);
      var totalOrders = deliveredOrders.Count;
      var averageOrderValue = totalOrders > 0 ? totalSales / totalOrders : 0;

      var salesByStatus = orders
          .GroupBy(o => o.Status)
          .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
          .ToList();

      _logger.LogInformation("Sales report generated: StartDate={StartDate}, EndDate={EndDate}, VendorId={VendorId}",
          startDate, endDate, vendorId);

      return new
      {
        startDate,
        endDate,
        totalSales = decimal.Round(totalSales, 2),
        totalOrders,
        averageOrderValue = decimal.Round((decimal)averageOrderValue, 2),
        ordersByStatus = salesByStatus
      };
    }

    public async Task<dynamic> GetSalesByCategoryAsync(DateTime startDate, DateTime endDate)
    {
      var salesByCategory = await _context.Orders
          .Where(o => o.OrderDate >= startDate && o.OrderDate <= endDate && o.Status == OrderStatus.Delivered)
          .SelectMany(o => o.OrderItems)
          .Include(oi => oi.Product)
              .ThenInclude(p => p.Category)
          .GroupBy(oi => oi.Product.Category!.NameEn)
          .Select(g => new
          {
            category = g.Key,
            totalSales = decimal.Round(g.Sum(oi => oi.UnitPrice * oi.Quantity), 2),
            totalItems = g.Sum(oi => oi.Quantity)
          })
          .OrderByDescending(x => x.totalSales)
          .ToListAsync();

      return new
      {
        startDate,
        endDate,
        data = salesByCategory
      };
    }

    public async Task<dynamic> GetVendorPerformanceAsync(int vendorId)
    {
      var vendor = await _context.Vendors.FindAsync(vendorId);
      if (vendor == null)
        return new { error = "Vendor not found" };

      var products = await _context.Products
          .Where(p => p.VendorId == vendorId)
          .Include(p => p.Reviews)
          .ToListAsync();

      var orders = await _context.Orders
          .Where(o => o.OrderItems.Any(oi => oi.Product.VendorId == vendorId))
          .Include(o => o.OrderItems)
              .ThenInclude(oi => oi.Product)
          .ToListAsync();

      var deliveredOrders = orders.Where(o => o.Status == OrderStatus.Delivered).ToList();
      var totalRevenue = decimal.Round(deliveredOrders.Sum(o => o.TotalAmount), 2);
      var totalOrders = deliveredOrders.Count;

      var allReviews = products.SelectMany(p => p.Reviews).ToList();
      var averageRating = allReviews.Any() ? Math.Round(allReviews.Average(r => r.Rating), 1) : 0;

      return new
      {
        vendorId,
        vendorName = vendor.StoreName,
        totalProducts = products.Count,
        totalRevenue,
        totalOrders,
        averageRating,
        totalReviews = allReviews.Count,
        activeProducts = products.Count(p => p.IsActive)
      };
    }

    public async Task<dynamic> GetCustomerInsightsAsync()
    {
      var totalCustomers = await _context.Users.CountAsync(u => u.Email != null);

      var activeCustomers = await _context.Orders
          .Select(o => o.UserId)
          .Distinct()
          .CountAsync();

      var totalSpent = await _context.Orders
          .Where(o => o.Status == OrderStatus.Delivered)
          .SumAsync(o => o.TotalAmount);

      var averageSpentPerCustomer = totalCustomers > 0 ? totalSpent / totalCustomers : 0;

      var topCustomers = await _context.Orders
          .Where(o => o.Status == OrderStatus.Delivered)
          .GroupBy(o => new { o.UserId, UserName = $"{o.User.FirstName} {o.User.LastName}" })
          .Select(g => new
          {
            userId = g.Key.UserId,
            name = g.Key.UserName,
            totalOrders = g.Count(),
            totalSpent = decimal.Round(g.Sum(o => o.TotalAmount), 2)
          })
          .Where(x => x.totalOrders > 0)
          .OrderByDescending(x => x.totalSpent)
          .Take(10)
          .ToListAsync();

      return new
      {
        totalCustomers,
        activeCustomers,
        totalSpent = decimal.Round(totalSpent, 2),
        averageSpentPerCustomer = decimal.Round((decimal)averageSpentPerCustomer, 2),
        topCustomers
      };
    }

    public async Task<dynamic> GetLowStockProductsAsync(int threshold = 10)
    {
      var lowStockProducts = await _context.Products
          .Where(p => p.StockQuantity <= threshold && p.IsActive)
          .Include(p => p.Vendor)
          .Include(p => p.Category)
          .Select(p => new
          {
            productId = p.Id,
            name = p.NameEn,
            nameAr = p.NameAr,
            currentStock = p.StockQuantity,
            price = p.Price,
            vendor = p.Vendor!.StoreName,
            category = p.Category!.NameEn
          })
          .OrderBy(p => p.currentStock)
          .ToListAsync();

      return new
      {
        threshold,
        count = lowStockProducts.Count,
        products = lowStockProducts
      };
    }

    public async Task<dynamic> GetRevenueByVendorAsync(DateTime startDate, DateTime endDate, int take = 10)
    {
      var revenueByVendor = await _context.Orders
          .Where(o => o.OrderDate >= startDate && o.OrderDate <= endDate && o.Status == OrderStatus.Delivered)
          .SelectMany(o => o.OrderItems)
          .Include(oi => oi.Product)
              .ThenInclude(p => p.Vendor)
          .GroupBy(oi => oi.Product.VendorId)
          .Select(g => new
          {
            vendorId = g.Key,
            vendorName = g.First().Product.Vendor!.StoreName,
            revenue = decimal.Round(g.Sum(oi => oi.UnitPrice * oi.Quantity), 2),
            orderCount = g.Select(oi => oi.OrderId).Distinct().Count()
          })
          .OrderByDescending(x => x.revenue)
          .Take(take)
          .ToListAsync();

      return new
      {
        startDate,
        endDate,
        data = revenueByVendor
      };
    }

    public async Task<dynamic> GetTopProductsAsync(DateTime startDate, DateTime endDate, int take = 10)
    {
      var topProducts = await _context.Orders
          .Where(o => o.OrderDate >= startDate && o.OrderDate <= endDate)
          .SelectMany(o => o.OrderItems)
          .Include(oi => oi.Product)
          .GroupBy(oi => oi.ProductId)
          .Select(g => new
          {
            productId = g.Key,
            name = g.First().Product.NameEn,
            totalSold = g.Sum(oi => oi.Quantity),
            totalRevenue = decimal.Round(g.Sum(oi => oi.UnitPrice * oi.Quantity), 2)
          })
          .OrderByDescending(x => x.totalSold)
          .Take(take)
          .ToListAsync();

      return new
      {
        startDate,
        endDate,
        data = topProducts
      };
    }

    public async Task<dynamic> GetOrderStatusSummaryAsync()
    {
      var statusSummary = await _context.Orders
          .GroupBy(o => o.Status)
          .Select(g => new
          {
            status = g.Key.ToString(),
            count = g.Count(),
            totalAmount = decimal.Round(g.Sum(o => o.TotalAmount), 2)
          })
          .ToListAsync();

      var totalOrders = statusSummary.Sum(x => x.count);
      var totalRevenue = statusSummary.Sum(x => x.totalAmount);

      return new
      {
        totalOrders,
        totalRevenue,
        byStatus = statusSummary
      };
    }

    public async Task<dynamic> GetUserTrendsAsync()
    {
      var now = DateTime.UtcNow;
      var last30Days = now.AddDays(-30);
      var last7Days = now.AddDays(-7);

      var newUsersLast30Days = await _context.Users
          .CountAsync(u => u.Id != null); // This should be tracked in entity, approximation here

      var newUsersLast7Days = await _context.Users
          .CountAsync(u => u.Id != null); // This should be tracked in entity, approximation here

      var totalOrders = await _context.Orders.CountAsync();
      var ordersLast30Days = await _context.Orders
          .CountAsync(o => o.OrderDate >= last30Days);

      var ordersLast7Days = await _context.Orders
          .CountAsync(o => o.OrderDate >= last7Days);

      return new
      {
        newUsersLast30Days,
        newUsersLast7Days,
        totalOrders,
        ordersLast30Days,
        ordersLast7Days
      };
    }
  }
}
