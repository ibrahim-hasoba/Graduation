using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs.Admin;
using System;
using System.Collections.Generic;
using System.Text;

namespace Graduation.BLL.Services.Implementations
{
    public class AdminService : IAdminService
    {
        private readonly DatabaseContext _context;

        public AdminService(DatabaseContext context)
        {
            _context = context;
        }

        public async Task<DashboardStatsDto> GetDashboardStatsAsync()
        {
            var today = DateTime.UtcNow.Date;
            var firstDayOfMonth = new DateTime(today.Year, today.Month, 1);

            var stats = new DashboardStatsDto
            {
                TotalUsers = await _context.Users.CountAsync(),
                TotalVendors = await _context.Vendors.CountAsync(),
                PendingVendors = await _context.Vendors.CountAsync(v => !v.IsApproved),
                ActiveVendors = await _context.Vendors.CountAsync(v => v.IsApproved && v.IsActive),
                TotalProducts = await _context.Products.CountAsync(),
                ActiveProducts = await _context.Products.CountAsync(p => p.IsActive),
                OutOfStockProducts = await _context.Products.CountAsync(p => p.StockQuantity == 0),
                TotalOrders = await _context.Orders.CountAsync(),
                PendingOrders = await _context.Orders.CountAsync(o => o.Status == OrderStatus.Pending),
                CompletedOrders = await _context.Orders.CountAsync(o => o.Status == OrderStatus.Delivered),
                CancelledOrders = await _context.Orders.CountAsync(o => o.Status == OrderStatus.Cancelled),
                TotalRevenue = await _context.Orders
                    .Where(o => o.Status == OrderStatus.Delivered)
                    .SumAsync(o => o.TotalAmount),
                MonthlyRevenue = await _context.Orders
                    .Where(o => o.Status == OrderStatus.Delivered && o.OrderDate >= firstDayOfMonth)
                    .SumAsync(o => o.TotalAmount),
                TotalCategories = await _context.Categories.CountAsync(c => c.IsActive),
                NewUsersToday = await _context.Users.CountAsync(u => u.LockoutEnd == null), // Approximation
                NewOrdersToday = await _context.Orders.CountAsync(o => o.OrderDate >= today)
            };

            return stats;
        }

        public async Task<List<RecentActivityDto>> GetRecentActivitiesAsync(int count = 10)
        {
            var activities = new List<RecentActivityDto>();

            // Recent orders
            var recentOrders = await _context.Orders
                .Include(o => o.User)
                .OrderByDescending(o => o.OrderDate)
                .Take(count / 2)
                .Select(o => new RecentActivityDto
                {
                    Type = "Order",
                    Description = $"New order #{o.OrderNumber} by {o.User.FirstName} {o.User.LastName}",
                    Timestamp = o.OrderDate,
                    Link = $"/admin/orders/{o.Id}"
                })
                .ToListAsync();

            activities.AddRange(recentOrders);

            // Recent vendor registrations
            var recentVendors = await _context.Vendors
                .OrderByDescending(v => v.CreatedAt)
                .Take(count / 2)
                .Select(v => new RecentActivityDto
                {
                    Type = "Vendor",
                    Description = $"New vendor registration: {v.StoreName}",
                    Timestamp = v.CreatedAt,
                    Link = $"/admin/vendors/{v.Id}"
                })
                .ToListAsync();

            activities.AddRange(recentVendors);

            return activities.OrderByDescending(a => a.Timestamp).Take(count).ToList();
        }

        public async Task<List<TopProductDto>> GetTopProductsAsync(int count = 10)
        {
            var topProducts = await _context.OrderItems
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

        public async Task<List<TopVendorDto>> GetTopVendorsAsync(int count = 10)
        {
            var topVendors = await _context.Vendors
                .Include(v => v.Products)
                    .ThenInclude(p => p.Reviews.Where(r => r.IsApproved))
                .Include(v => v.Orders.Where(o => o.Status == OrderStatus.Delivered))
                .Where(v => v.IsApproved)
                .Select(v => new TopVendorDto
                {
                    Id = v.Id,
                    StoreName = v.StoreName,
                    StoreNameAr = v.StoreNameAr,
                    TotalProducts = v.Products.Count(p => p.IsActive),
                    TotalOrders = v.Orders.Count,
                    TotalRevenue = v.Orders.Sum(o => o.TotalAmount),
                    AverageRating = v.Products
                        .SelectMany(p => p.Reviews)
                        .Any()
                        ? v.Products.SelectMany(p => p.Reviews).Average(r => r.Rating)
                        : 0
                })
                .OrderByDescending(v => v.TotalRevenue)
                .Take(count)
                .ToListAsync();

            return topVendors;
        }

        public async Task<SalesChartDto> GetSalesChartDataAsync()
        {
            var today = DateTime.UtcNow.Date;
            var last30Days = today.AddDays(-29);
            var last12Months = today.AddMonths(-11);

            // Daily sales for last 30 days
            var dailySales = await _context.Orders
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

            // Monthly sales for last 12 months
            var monthlySales = await _context.Orders
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
            var totalUsers = await _context.Users.CountAsync();
            var verifiedUsers = await _context.Users.CountAsync(u => u.EmailConfirmed);

            // Get role counts
            var customerRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Customer");
            var vendorRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Vendor");
            var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Admin");

            var customersCount = customerRole != null
                ? await _context.UserRoles.CountAsync(ur => ur.RoleId == customerRole.Id)
                : 0;
            var vendorsCount = vendorRole != null
                ? await _context.UserRoles.CountAsync(ur => ur.RoleId == vendorRole.Id)
                : 0;
            var adminsCount = adminRole != null
                ? await _context.UserRoles.CountAsync(ur => ur.RoleId == adminRole.Id)
                : 0;

            return new UserStatsDto
            {
                TotalUsers = totalUsers,
                CustomersCount = customersCount,
                VendorsCount = vendorsCount,
                AdminsCount = adminsCount,
                VerifiedUsers = verifiedUsers,
                UnverifiedUsers = totalUsers - verifiedUsers,
                MonthlyGrowth = new List<UserGrowthDto>() // Can be implemented later
            };
        }
    }
}
