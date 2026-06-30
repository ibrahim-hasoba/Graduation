using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Entities;
using Graduation.DAL.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Graduation.BLL.DTOs.Admin;

namespace Graduation.BLL.Services.Implementations
{
    public class AdminService : IAdminService
    {
        private readonly IUnitOfWork _uow;

        public AdminService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<DashboardStatsDto> GetDashboardStatsAsync()
        {
            var today = DateTime.UtcNow.Date;
            var firstDayOfMonth = new DateTime(today.Year, today.Month, 1);

            var repo = _uow.Repository<AppUser>();

            var stats = new DashboardStatsDto
            {
                TotalUsers = await repo.CountAsync(),
                TotalVendors = await _uow.Repository<Vendor>().CountAsync(),
                PendingVendors = await _uow.Repository<Vendor>().CountAsync(v => v.ApprovalStatus == VendorApprovalStatus.Pending),
                ActiveVendors = await _uow.Repository<Vendor>().CountAsync(v => v.ApprovalStatus == VendorApprovalStatus.Approved && v.IsActive),
                TotalProducts = await _uow.Repository<Product>().CountAsync(),
                ActiveProducts = await _uow.Repository<Product>().CountAsync(p => p.IsActive),
                OutOfStockProducts = await _uow.Repository<Product>().CountAsync(p => p.StockQuantity == 0),
                TotalOrders = await _uow.Repository<Order>().CountAsync(),
                PendingOrders = await _uow.Repository<Order>().CountAsync(o => o.Status == OrderStatus.Pending),
                CompletedOrders = await _uow.Repository<Order>().CountAsync(o => o.Status == OrderStatus.Delivered),
                CancelledOrders = await _uow.Repository<Order>().CountAsync(o => o.Status == OrderStatus.Cancelled),
                TotalRevenue = await _uow.Repository<Order>().Query()
                    .Where(o => o.Status == OrderStatus.Delivered)
                    .SumAsync(o => o.TotalAmount),
                MonthlyRevenue = await _uow.Repository<Order>().Query()
                    .Where(o => o.Status == OrderStatus.Delivered && o.OrderDate >= firstDayOfMonth)
                    .SumAsync(o => o.TotalAmount),
                TotalCategories = await _uow.Repository<Category>().CountAsync(c => c.Status == CategoryStatus.Active),

                NewUsersToday = await repo.Query().CountAsync(u => u.CreatedAt >= today),

                NewOrdersToday = await _uow.Repository<Order>().Query().CountAsync(o => o.OrderDate >= today)
            };

            return stats;
        }

        public async Task<List<TopVendorDto>> GetTopVendorsAsync(int count = 10)
        {
            var approvedVendors = await _uow.Repository<Vendor>().Query()
                .Where(v => v.ApprovalStatus == VendorApprovalStatus.Approved)
                .Select(v => new
                {
                    v.Id,
                    v.StoreName,
                    v.StoreNameAr,
                    TotalProducts = v.Products.Count(p => p.IsActive)
                })
                .ToListAsync();

            var vendorIds = approvedVendors.Select(v => v.Id).ToList();

            var vendorOrderStats = await _uow.Repository<OrderItem>().Query()
                .Where(oi => vendorIds.Contains(oi.Product.VendorId))
                .GroupBy(oi => oi.Product.VendorId)
                .Select(g => new
                {
                    VendorId = g.Key,
                    TotalOrders = g.Select(oi => oi.OrderId).Distinct().Count(),
                    TotalRevenue = g.Sum(oi => (decimal?)oi.TotalPrice) ?? 0m
                })
                .ToListAsync();

            var vendorRatingStats = await _uow.Repository<ProductReview>().Query()
                .Where(r => r.IsApproved && vendorIds.Contains(r.Product.VendorId))
                .GroupBy(r => r.Product.VendorId)
                .Select(g => new
                {
                    VendorId = g.Key,
                    AverageRating = g.Average(r => (double)r.Rating)
                })
                .ToListAsync();

            var orderStatsDict = vendorOrderStats.ToDictionary(x => x.VendorId);
            var ratingDict = vendorRatingStats.ToDictionary(x => x.VendorId);

            return approvedVendors
                .Select(v => new TopVendorDto
                {
                    Id = v.Id,
                    StoreName = v.StoreName,
                    StoreNameAr = v.StoreNameAr,
                    TotalProducts = v.TotalProducts,
                    TotalOrders = orderStatsDict.TryGetValue(v.Id, out var os) ? os.TotalOrders : 0,
                    TotalRevenue = orderStatsDict.TryGetValue(v.Id, out var rs) ? rs.TotalRevenue : 0m,
                    AverageRating = ratingDict.TryGetValue(v.Id, out var rat) ? Math.Round(rat.AverageRating, 1) : 0.0
                })
                .OrderByDescending(v => v.TotalRevenue)
                .Take(count)
                .ToList();
        }

        public async Task<SalesChartDto> GetSalesChartDataAsync()
        {
            var today = DateTime.UtcNow.Date;
            var last30Days = today.AddDays(-29);
            var last12Months = today.AddMonths(-11);

            var dailySales = await _uow.Repository<Order>().Query()
                .Where(o => o.OrderDate >= last30Days && o.Status == OrderStatus.Delivered)
                .GroupBy(o => o.OrderDate.Date)
                .Select(g => new ChartDataPoint
                {
                    Label = g.Key.ToString("MMM dd"),
                    Value = g.Sum(o => o.TotalAmount),
                    Count = g.Count()
                })
                .OrderBy(x => x.Label)
                .ToListAsync();

            var monthlySales = await _uow.Repository<Order>().Query()
                .Where(o => o.OrderDate >= last12Months && o.Status == OrderStatus.Delivered)
                .GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month })
                .Select(g => new ChartDataPoint
                {
                    Label = $"{g.Key.Year}-{g.Key.Month:00}",
                    Value = g.Sum(o => o.TotalAmount),
                    Count = g.Count()
                })
                .OrderBy(x => x.Label)
                .ToListAsync();

            return new SalesChartDto
            {
                Daily = dailySales,
                Monthly = monthlySales
            };
        }

        public async Task<UserStatsDto> GetUserStatsAsync()
        {
            var totalUsers = await _uow.Repository<AppUser>().CountAsync();
            var verifiedUsers = await _uow.Repository<AppUser>().Query().CountAsync(u => u.EmailConfirmed);

            var customerRole = await _uow.Repository<IdentityRole>().Query().FirstOrDefaultAsync(r => r.Name == "Customer");
            var vendorRole = await _uow.Repository<IdentityRole>().Query().FirstOrDefaultAsync(r => r.Name == "Vendor");
            var adminRole = await _uow.Repository<IdentityRole>().Query().FirstOrDefaultAsync(r => r.Name == "Admin");

            var customersCount = customerRole != null
                ? await _uow.Repository<IdentityUserRole<string>>().Query().CountAsync(ur => ur.RoleId == customerRole.Id)
                : 0;
            var vendorsCount = vendorRole != null
                ? await _uow.Repository<IdentityUserRole<string>>().Query().CountAsync(ur => ur.RoleId == vendorRole.Id)
                : 0;
            var adminsCount = adminRole != null
                ? await _uow.Repository<IdentityUserRole<string>>().Query().CountAsync(ur => ur.RoleId == adminRole.Id)
                : 0;

            return new UserStatsDto
            {
                TotalUsers = totalUsers,
                CustomersCount = customersCount,
                VendorsCount = vendorsCount,
                AdminsCount = adminsCount,
                VerifiedUsers = verifiedUsers,
                UnverifiedUsers = totalUsers - verifiedUsers,
                MonthlyGrowth = new List<UserGrowthDto>()
            };
        }

        public async Task<List<TopProductDto>> GetTopProductsAsync(int count = 10)
        {
            var topProducts = await _uow.Repository<OrderItem>().Query()
                .AsNoTracking()
                .Include(oi => oi.Product)
                    .ThenInclude(p => p.Vendor)
                .Include(oi => oi.Product)
                    .ThenInclude(p => p.Images)
                .GroupBy(oi => oi.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    Product = g.First().Product,
                    TotalSales = g.Sum(oi => oi.Quantity),
                    Revenue = g.Sum(oi => oi.TotalPrice)
                })
                .OrderByDescending(x => x.TotalSales)
                .Take(count)
                .ToListAsync();

            return topProducts.Select(tp => new TopProductDto
            {
                Id = tp.ProductId,
                NameEn = tp.Product.NameEn,
                NameAr = tp.Product.NameAr,
                ImageUrl = tp.Product.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
                    ?? tp.Product.Images.FirstOrDefault()?.ImageUrl,
                TotalSales = tp.TotalSales,
                Revenue = tp.Revenue,
                VendorName = tp.Product.Vendor.StoreName
            }).ToList();
        }
    }
}
