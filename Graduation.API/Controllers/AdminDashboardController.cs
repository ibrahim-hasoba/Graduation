using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Graduation.API.Controllers
{
    [Route("api/admin/dashboard")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminDashboardController : BaseController
    {
        private readonly IAdminService _adminService;
        private readonly IActivityLogService _activityLog;

        public AdminDashboardController(
            IAdminService adminService,
            IActivityLogService activityLog,
            ILanguageService lang)
            : base(lang)
        {
            _adminService = adminService;
            _activityLog = activityLog;
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            var stats = await _adminService.GetDashboardStatsAsync();
            return OkResult(data: stats);
        }

        [HttpGet("activities")]
        public async Task<IActionResult> GetRecentActivities([FromQuery] int count = 10)
        {
            var activities = await _activityLog.GetRecentActivitiesAsync(count);
            return OkResult(data: activities);
        }

        [HttpGet("top-products")]
        public async Task<IActionResult> GetTopProducts([FromQuery] int count = 10)
        {
            var products = await _adminService.GetTopProductsAsync(count);
            return OkResult(data: products);
        }

        [HttpGet("top-vendors")]
        public async Task<IActionResult> GetTopVendors([FromQuery] int count = 10)
        {
            var vendors = await _adminService.GetTopVendorsAsync(count);
            return OkResult(data: vendors);
        }

        [HttpGet("sales-chart")]
        public async Task<IActionResult> GetSalesChart()
        {
            var chartData = await _adminService.GetSalesChartDataAsync();
            return OkResult(data: chartData);
        }

        [HttpGet("user-stats")]
        public async Task<IActionResult> GetUserStats()
        {
            var stats = await _adminService.GetUserStatsAsync();
            return OkResult(data: stats);
        }
    }

    [Route("api/admin/stats")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminStatsController : BaseController
    {
        private readonly DatabaseContext _context;

        public AdminStatsController(DatabaseContext context, ILanguageService lang) : base(lang)
        {
            _context = context;
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSystemSummary()
        {
            var totalUsers = await _context.Users.CountAsync();
            var totalVendors = await _context.Vendors.CountAsync();
            var totalProducts = await _context.Products.CountAsync();
            var totalOrders = await _context.Orders.CountAsync();
            var totalRevenue = await _context.Orders
                .Where(o => o.Status == OrderStatus.Delivered)
                .SumAsync(o => o.TotalAmount);

            var today = DateTime.UtcNow.Date;
            var todayOrders = await _context.Orders.CountAsync(o => o.OrderDate >= today);
            var todayRevenue = await _context.Orders
                .Where(o => o.OrderDate >= today && o.Status == OrderStatus.Delivered)
                .SumAsync(o => o.TotalAmount);

            return OkResult(data: new
            {
                totalUsers,
                totalVendors,
                totalProducts,
                totalOrders,
                totalRevenue,
                todayOrders,
                todayRevenue,
                averageOrderValue = totalOrders > 0 ? totalRevenue / totalOrders : 0
            });
        }
    }
}
