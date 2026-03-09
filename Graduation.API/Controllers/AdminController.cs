using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs.Admin;
using Shared.DTOs.Category;
using Shared.Errors;


namespace Graduation.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminService;
        private readonly ICategoryService _categoryService;
        private readonly IReportService _reportService;
        private readonly DatabaseContext _context;
        private readonly UserManager<AppUser> _userManager;

        public AdminController(
            IAdminService adminService,
            ICategoryService categoryService,
            IReportService reportService,
            DatabaseContext context,
            UserManager<AppUser> userManager)
        {
            _adminService = adminService;
            _categoryService = categoryService;
            _reportService = reportService;
            _context = context;
            _userManager = userManager;
        }

        /// <summary>
        /// Get dashboard statistics
        /// </summary>
        [HttpGet("dashboard/stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            var stats = await _adminService.GetDashboardStatsAsync();
            return Ok(new ApiResult(data: stats));
        }

        /// <summary>
        /// Get recent activities
        /// </summary>
        [HttpGet("dashboard/activities")]
        public async Task<IActionResult> GetRecentActivities([FromQuery] int count = 10)
        {
            var activities = await _adminService.GetRecentActivitiesAsync(count);
            return Ok(new ApiResult(data: activities));
        }

        /// <summary>
        /// Get top selling products
        /// </summary>
        [HttpGet("dashboard/top-products")]
        public async Task<IActionResult> GetTopProducts([FromQuery] int count = 10)
        {
            var products = await _adminService.GetTopProductsAsync(count);
            return Ok(new ApiResult(data: products));
        }

        /// <summary>
        /// Get top vendors by revenue
        /// </summary>
        [HttpGet("dashboard/top-vendors")]
        public async Task<IActionResult> GetTopVendors([FromQuery] int count = 10)
        {
            var vendors = await _adminService.GetTopVendorsAsync(count);
            return Ok(new ApiResult(data: vendors));
        }

        /// <summary>
        /// Get sales chart data
        /// </summary>
        [HttpGet("dashboard/sales-chart")]
        public async Task<IActionResult> GetSalesChart()
        {
            var chartData = await _adminService.GetSalesChartDataAsync();
            return Ok(new ApiResult(data: chartData));
        }

        /// <summary>
        /// Get user statistics
        /// </summary>
        [HttpGet("dashboard/user-stats")]
        public async Task<IActionResult> GetUserStats()
        {
            var stats = await _adminService.GetUserStatsAsync();
            return Ok(new ApiResult(data: stats));
        }

        /// <summary>
        /// Get all users with pagination
        /// </summary>
        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? role = null)
        {
            var query = _context.Users.AsQueryable();

            if (!string.IsNullOrEmpty(role))
            {
                var roleEntity = await _context.Roles.FirstOrDefaultAsync(r => r.Name == role);
                if (roleEntity != null)
                {
                    var userIds = await _context.UserRoles
                        .Where(ur => ur.RoleId == roleEntity.Id)
                        .Select(ur => ur.UserId)
                        .ToListAsync();

                    query = query.Where(u => userIds.Contains(u.Id));
                }
            }

            var totalCount = await query.CountAsync();

            var users = await query
                .OrderByDescending(u => u.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new
                {
                    id = u.Id,
                    email = u.Email,
                    firstName = u.FirstName,
                    lastName = u.LastName,
                    emailConfirmed = u.EmailConfirmed,
                    phoneNumber = u.PhoneNumber,
                    lockoutEnabled = u.LockoutEnabled
                })
                .ToListAsync();

            return Ok(new ApiResult(data: new
            {
                users,
                totalCount,
                pageNumber,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            }));
        }

        /// <summary>
        /// Delete user account
        /// </summary>
        [HttpDelete("users/{userId}")]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                throw new NotFoundException("User not found");

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
                throw new BadRequestException("Failed to delete user");

            return Ok(new ApiResult(message: "User deleted successfully"));
        }

        /// <summary>
        /// Lock/Unlock user account
        /// </summary>
        [HttpPost("users/{userId}/toggle-lock")]
        public async Task<IActionResult> ToggleUserLock(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                throw new NotFoundException("User not found");

            if (user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow)
            {
                // Unlock
                await _userManager.SetLockoutEndDateAsync(user, null);
                return Ok(new ApiResult(data: new { locked = false }, message: "User unlocked successfully"));
            }
            else
            {
                // Lock for 100 years (permanent lock)
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
                return Ok(new ApiResult(data: new { locked = true }, message: "User locked successfully"));
            }
        }
        [HttpPut("users/{userId}")]
        public async Task<IActionResult> UpdateUser(string userId, [FromBody] UpdateUserDto dto)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                throw new NotFoundException("User not found");

            user.FirstName = dto.FirstName ?? user.FirstName;
            user.LastName = dto.LastName ?? user.LastName;
            user.PhoneNumber = dto.PhoneNumber ?? user.PhoneNumber;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                throw new BadRequestException(result.Errors.First().Description);

            return Ok(new ApiResult(data: new
            {
                id = user.Id,
                email = user.Email,
                firstName = user.FirstName,
                lastName = user.LastName,
                phoneNumber = user.PhoneNumber,
                lockoutEnabled = user.LockoutEnabled

            }, message: "User updated successfully"));
        }
        /// <summary>
        /// Admin resets a user's password
        /// </summary>
        [HttpPost("users/{userId}/reset-password")]
        public async Task<IActionResult> ResetUserPassword(string userId, [FromBody] AdminResetPasswordDto dto)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                throw new NotFoundException("User not found");

            var isTargetAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            if (isTargetAdmin)
                throw new BadRequestException("Admins cannot reset other admin passwords");

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            var result = await _userManager.ResetPasswordAsync(user, token, dto.NewPassword);

            if (!result.Succeeded)
                throw new BadRequestException(string.Join(", ", result.Errors.Select(e => e.Description)));

            await _userManager.UpdateSecurityStampAsync(user);

            return Ok(new ApiResult(message: "Password reset successfully"));
        }

        /// <summary>
        /// Get all orders with filters
        /// </summary>
        [HttpGet("orders")]
        public async Task<IActionResult> GetAllOrders(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] OrderStatus? status = null)
        {
            var query = _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.Vendor)
                .AsQueryable();

            if (status.HasValue)
                query = query.Where(o => o.Status == status.Value);

            var totalCount = await query.CountAsync();

            var orders = await query
                .OrderByDescending(o => o.OrderDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new
                {
                    id = o.Id,
                    orderNumber = o.OrderNumber,
                    customerName = $"{o.User.FirstName} {o.User.LastName}",
                    customerEmail = o.User.Email,
                    vendorName = o.OrderItems.Select(oi => oi.Product.Vendor.StoreName).FirstOrDefault(),
                    totalAmount = o.TotalAmount,
                    status = o.Status.ToString(),
                    paymentStatus = o.PaymentStatus.ToString(),
                    orderDate = o.OrderDate,
                    itemsCount = o.OrderItems.Count
                })
                .ToListAsync();

            return Ok(new ApiResult(data: new
            {
                orders,
                totalCount,
                pageNumber,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            }));
        }

        /// <summary>
        /// Get system statistics summary
        /// </summary>
        [HttpGet("stats/summary")]
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

            return Ok(new ApiResult(data: new
            {
                totalUsers,
                totalVendors,
                totalProducts,
                totalOrders,
                totalRevenue,
                todayOrders,
                todayRevenue,
                averageOrderValue = totalOrders > 0 ? totalRevenue / totalOrders : 0
            }));
        }

        

        
        [HttpPost("categories")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryDto dto)
        {
            var category = await _categoryService.CreateCategoryAsync(dto);
            return CreatedAtAction(nameof(GetCategoryById), new { id = category.Id },
                new ApiResult(data: category));
        }

        /// <summary>Get all categories with hierarchy</summary>
        [HttpGet("categories")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetAllCategories([FromQuery] bool includeInactive = false)
        {
            var categories = await _categoryService.GetAllCategoriesAsync(includeInactive);
            return Ok(new ApiResult(data: categories));
        }

        /// <summary>Get category by ID</summary>
        [HttpGet("categories/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetCategoryById(int id)
        {
            var category = await _categoryService.GetCategoryByIdAsync(id);
            return Ok(new ApiResult(data: category));
        }

        /// <summary>Update category</summary>
        [HttpPut("categories/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UpdateCategory(int id, [FromBody] UpdateCategoryDto dto)
        {
            var category = await _categoryService.UpdateCategoryAsync(id, dto);
            return Ok(new ApiResult(data: category));
        }

        /// <summary>Delete category (soft delete)</summary>
        [HttpDelete("categories/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            await _categoryService.DeleteCategoryAsync(id);
            return Ok(new ApiResult(message: "Category deleted successfully"));
        }

        

        #region Reports

        /// <summary>
        /// Get sales report for date range
        /// </summary>
        [HttpGet("reports/sales")]
        public async Task<IActionResult> GetSalesReport(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate,
            [FromQuery] int? vendorId = null)
        {
            if (startDate == DateTime.MinValue || endDate == DateTime.MinValue)
                return BadRequest(new ApiResponse(400, "Start date and end date are required"));

            var report = await _reportService.GetSalesReportAsync(startDate, endDate, vendorId);
            return Ok(new ApiResult(data: report));
        }

        /// <summary>
        /// Get sales breakdown by category
        /// </summary>
        [HttpGet("reports/sales-by-category")]
        public async Task<IActionResult> GetSalesByCategory(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate)
        {
            if (startDate == DateTime.MinValue || endDate == DateTime.MinValue)
                return BadRequest(new ApiResponse(400, "Start date and end date are required"));

            var report = await _reportService.GetSalesByCategoryAsync(startDate, endDate);
            return Ok(new ApiResult(data: report));
        }

        /// <summary>
        /// Get vendor performance metrics
        /// </summary>
        [HttpGet("reports/vendor-performance/{vendorId}")]
        public async Task<IActionResult> GetVendorPerformance(int vendorId)
        {
            var report = await _reportService.GetVendorPerformanceAsync(vendorId);
            return Ok(new ApiResult(data: report));
        }

        /// <summary>
        /// Get customer insights and analytics
        /// </summary>
        [HttpGet("reports/customer-insights")]
        public async Task<IActionResult> GetCustomerInsights()
        {
            var report = await _reportService.GetCustomerInsightsAsync();
            return Ok(new ApiResult(data: report));
        }

        /// <summary>
        /// Get low stock products
        /// </summary>
        [HttpGet("reports/low-stock")]
        public async Task<IActionResult> GetLowStockProducts([FromQuery] int threshold = 10)
        {
            var report = await _reportService.GetLowStockProductsAsync(threshold);
            return Ok(new ApiResult(data: report));
        }

        /// <summary>
        /// Get revenue by vendor for date range
        /// </summary>
        [HttpGet("reports/revenue-by-vendor")]
        public async Task<IActionResult> GetRevenueByVendor(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate,
            [FromQuery] int take = 10)
        {
            if (startDate == DateTime.MinValue || endDate == DateTime.MinValue)
                return BadRequest(new ApiResponse(400, "Start date and end date are required"));

            var report = await _reportService.GetRevenueByVendorAsync(startDate, endDate, take);
            return Ok(new ApiResult(data: report));
        }

        /// <summary>
        /// Get top selling products
        /// </summary>
        [HttpGet("reports/top-products")]
        public async Task<IActionResult> GetTopProducts(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate,
            [FromQuery] int take = 10)
        {
            if (startDate == DateTime.MinValue || endDate == DateTime.MinValue)
                return BadRequest(new ApiResponse(400, "Start date and end date are required"));

            var report = await _reportService.GetTopProductsAsync(startDate, endDate, take);
            return Ok(new ApiResult(data: report));
        }

        /// <summary>
        /// Get order status summary
        /// </summary>
        [HttpGet("reports/order-status-summary")]
        public async Task<IActionResult> GetOrderStatusSummary()
        {
            var report = await _reportService.GetOrderStatusSummaryAsync();
            return Ok(new ApiResult(data: report));
        }

        /// <summary>
        /// Get user trends and engagement metrics
        /// </summary>
        [HttpGet("reports/user-trends")]
        public async Task<IActionResult> GetUserTrends()
        {
            var report = await _reportService.GetUserTrendsAsync();
            return Ok(new ApiResult(data: report));
        }

        #endregion
    }
}
