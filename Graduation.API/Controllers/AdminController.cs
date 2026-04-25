using Graduation.BLL.Services.Implementations;
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
using Shared.DTOs.Product;
using Shared.Errors;
using System.Security.Claims;
using System.Text;

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
        private readonly ICodeLookupService _codeLookup;
        private readonly ICodeAssignmentService _codeAssignment;
        private readonly IImageService _imageService;
        private readonly IProductService _productService;
        private readonly IOrderService _orderService;

        public AdminController(
            IAdminService adminService,
            ICategoryService categoryService,
            IReportService reportService,
            DatabaseContext context,
            UserManager<AppUser> userManager,
            ICodeLookupService codeLookup,
            ICodeAssignmentService codeAssignment,
            IImageService imageService,
            IProductService productService,
            IOrderService orderService)
        {
            _adminService = adminService;
            _categoryService = categoryService;
            _reportService = reportService;
            _context = context;
            _userManager = userManager;
            _codeLookup = codeLookup;
            _codeAssignment = codeAssignment;
            _imageService = imageService;
            _productService = productService;
            _orderService = orderService;
        }

        [HttpGet("dashboard/stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            var stats = await _adminService.GetDashboardStatsAsync();
            return Ok(new ApiResult(data: stats));
        }

        [HttpGet("dashboard/activities")]
        public async Task<IActionResult> GetRecentActivities([FromQuery] int count = 10)
        {
            var activities = await _adminService.GetRecentActivitiesAsync(count);
            return Ok(new ApiResult(data: activities));
        }

        [HttpGet("dashboard/top-products")]
        public async Task<IActionResult> GetTopProducts([FromQuery] int count = 10)
        {
            var products = await _adminService.GetTopProductsAsync(count);
            return Ok(new ApiResult(data: products));
        }

        [HttpGet("dashboard/top-vendors")]
        public async Task<IActionResult> GetTopVendors([FromQuery] int count = 10)
        {
            var vendors = await _adminService.GetTopVendorsAsync(count);
            return Ok(new ApiResult(data: vendors));
        }

        [HttpGet("dashboard/sales-chart")]
        public async Task<IActionResult> GetSalesChart()
        {
            var chartData = await _adminService.GetSalesChartDataAsync();
            return Ok(new ApiResult(data: chartData));
        }

        [HttpGet("dashboard/user-stats")]
        public async Task<IActionResult> GetUserStats()
        {
            var stats = await _adminService.GetUserStatsAsync();
            return Ok(new ApiResult(data: stats));
        }

        [HttpGet("users/{userCode}")]
        public async Task<IActionResult> GetUser(string userCode)
        {
            var userId = await _codeLookup.ResolveUserIdAsync(userCode);
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) throw new NotFoundException("User not found");
            var fullProfilePictureUrl = _imageService.GetFullImageUrl(user.ProfilePictureUrl!);

            return Ok(new ApiResult(data: new
            {
                id = user.Id,
                userCode = user.Code,
                email = user.Email,
                firstName = user.FirstName,
                lastName = user.LastName,
                emailConfirmed = user.EmailConfirmed,
                phoneNumber = user.PhoneNumber,
                ProfilePicture = fullProfilePictureUrl,
                createdAt = user.CreatedAt,
                isLocked = user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow,
            }));
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? role = null)
        {
            var query = _context.Users.AsQueryable();

            if (!string.IsNullOrEmpty(role))
            {
                var normalizedRole = role.ToUpper();
                var roleEntity = await _context.Roles
                    .FirstOrDefaultAsync(r => r.NormalizedName == normalizedRole);

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

            var rawUsers = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new
                {
                    id = u.Id,
                    email = u.Email,
                    code = u.Code,
                    firstName = u.FirstName,
                    lastName = u.LastName,
                    emailConfirmed = u.EmailConfirmed,
                    phoneNumber = u.PhoneNumber,
                    profilePictureUrl = u.ProfilePictureUrl,
                    createdAt = u.CreatedAt,
                    updatedAt = u.UpdatedAt,
                    lockoutEnabled = u.LockoutEnabled,
                    isLocked = u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow
                })
                .ToListAsync();

            var users = rawUsers.Select(u => new
            {
                u.code,
                u.email,
                u.firstName,
                u.lastName,
                u.emailConfirmed,
                u.phoneNumber,
                u.isLocked,
                profilePicture = _imageService.GetFullImageUrl(u.profilePictureUrl!),
                createdAt = u.createdAt == DateTime.MinValue ? (DateTime?)null : u.createdAt,
                updatedAt = u.updatedAt == DateTime.MinValue ? (DateTime?)null : u.updatedAt,
            }).ToList();

            return Ok(new ApiResult(data: new
            {
                users,
                totalCount,
                pageNumber,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            }));
        }

        [HttpDelete("users/{userCode}")]
        public async Task<IActionResult> DeleteUser(string userCode)
        {
            var requestingAdminId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                                 ?? User.FindFirstValue("userId");

            var userId = await _codeLookup.ResolveUserIdAsync(userCode);

            if (requestingAdminId == userId)
                throw new BadRequestException("You cannot delete your own admin account.");

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                throw new NotFoundException("User not found");

            await _orderService.HandleUserAccountDeletionAsync(userId);

            await _context.CartItems.Where(c => c.UserId == userId).ExecuteDeleteAsync();
            await _context.Wishlists.Where(w => w.UserId == userId).ExecuteDeleteAsync();
            await _context.UserAddresses.Where(a => a.UserId == userId).ExecuteDeleteAsync();
            await _context.Notifications.Where(n => n.UserId == userId).ExecuteDeleteAsync();

            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Any())
                await _userManager.RemoveFromRolesAsync(user, roles);

            var claims = await _userManager.GetClaimsAsync(user);
            if (claims.Any())
                await _userManager.RemoveClaimsAsync(user, claims);

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
                throw new BadRequestException(
                    string.Join(", ", result.Errors.Select(e => e.Description)));

            return Ok(new ApiResult(message: "User deleted successfully"));
        }

        [HttpPost("users/{userCode}/toggle-lock")]
        public async Task<IActionResult> ToggleUserLock(string userCode)
        {
            var userId = await _codeLookup.ResolveUserIdAsync(userCode);
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) throw new NotFoundException("User not found");

            if (user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow)
            {
                await _userManager.SetLockoutEndDateAsync(user, null);
                return Ok(new ApiResult(data: new { locked = false }, message: "User unlocked successfully"));
            }
            else
            {
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
                return Ok(new ApiResult(data: new { locked = true }, message: "User locked successfully"));
            }
        }

        [HttpPost("users")]
        public async Task<IActionResult> CreateUser([FromBody] AdminCreateUserDto dto)
        {
            var existingUser = await _userManager.FindByEmailAsync(dto.Email);
            if (existingUser != null)
                throw new BadRequestException("Email already exists");

            var user = new AppUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                PhoneNumber = dto.PhoneNumber,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            var result = await _userManager.CreateAsync(user, dto.Password);

            if (!result.Succeeded)
                throw new BadRequestException(
                    string.Join(", ", result.Errors.Select(e => e.Description)));

            if (!await _context.Roles.AnyAsync(r => r.Name == dto.Role))
                throw new NotFoundException("Role not found");

            await _userManager.AddToRoleAsync(user, dto.Role);
            await _codeAssignment.AssignUserCodeAsync(user);

            return Ok(new ApiResult(data: new
            {
                userCode = user.Code,
                email = user.Email,
                firstName = user.FirstName,
                lastName = user.LastName,
                phone = user.PhoneNumber,
                createdAt = user.CreatedAt,
                updatedAt = user.UpdatedAt,
                role = dto.Role,
                isLocked = user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow
            }, message: "User created successfully"));
        }

        [HttpPut("users/{userCode}")]
        public async Task<IActionResult> UpdateUser(string userCode, [FromBody] UpdateUserDto dto)
        {
            var userId = await _codeLookup.ResolveUserIdAsync(userCode);
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) throw new NotFoundException("User not found");

            user.FirstName = dto.FirstName ?? user.FirstName;
            user.LastName = dto.LastName ?? user.LastName;
            user.PhoneNumber = dto.PhoneNumber ?? user.PhoneNumber;
            user.UpdatedAt = DateTime.UtcNow;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                throw new BadRequestException(result.Errors.First().Description);

            return Ok(new ApiResult(data: new
            {
                userCode = user.Code,
                email = user.Email,
                firstName = user.FirstName,
                lastName = user.LastName,
                phoneNumber = user.PhoneNumber,
                updatedAt = user.UpdatedAt,
                isLocked = user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow
            }, message: "User updated successfully"));
        }

        [HttpPost("users/{userCode}/reset-password")]
        public async Task<IActionResult> ResetUserPassword(string userCode, [FromBody] AdminResetPasswordDto dto)
        {
            var userId = await _codeLookup.ResolveUserIdAsync(userCode);
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) throw new NotFoundException("User not found");

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

        [HttpGet("users/export")]
        public async Task<IActionResult> ExportUsers([FromQuery] string? role = null)
        {
            var query = _context.Users.AsQueryable();

            if (!string.IsNullOrEmpty(role))
            {
                var normalizedRole = role.ToUpper();
                var roleEntity = await _context.Roles
                    .FirstOrDefaultAsync(r => r.NormalizedName == normalizedRole);

                if (roleEntity != null)
                {
                    var userIds = await _context.UserRoles
                        .Where(ur => ur.RoleId == roleEntity.Id)
                        .Select(ur => ur.UserId)
                        .ToListAsync();

                    query = query.Where(u => userIds.Contains(u.Id));
                }
            }

            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .Select(u => new
                {
                    u.FirstName,
                    u.LastName,
                    u.Email,
                    u.PhoneNumber,
                    u.EmailConfirmed,
                    u.CreatedAt,
                    u.UpdatedAt
                })
                .ToListAsync();

            var csv = new StringBuilder();
            csv.AppendLine("First Name,Last Name,Email,Phone,Verified,Created At,Updated At");

            foreach (var u in users)
            {
                csv.AppendLine(
                    $"{u.FirstName}," +
                    $"{u.LastName}," +
                    $"{u.Email}," +
                    $"{u.PhoneNumber ?? "--"}," +
                    $"{(u.EmailConfirmed ? "Yes" : "No")}," +
                    $"{u.CreatedAt:dd-MM-yyyy}," +
                    $"{(u.UpdatedAt == DateTime.MinValue ? "--" : u.UpdatedAt.ToString("dd-MM-yyyy"))}");
            }

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"users-export-{DateTime.UtcNow:yyyyMMdd}.csv");
        }

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
                    vendorNames = o.OrderItems
                        .Select(oi => oi.Product.Vendor.StoreName)
                        .Distinct()
                        .ToList(),
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
        public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryDto dto)
        {
            var category = await _categoryService.CreateCategoryAsync(dto);
            return CreatedAtAction(
                nameof(GetCategoryByCode),
                new { categoryCode = category.Code },
                new ApiResult(data: category));
        }

        [HttpGet("categories")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllCategories([FromQuery] CategoryQueryDto query)
        {
            var result = await _categoryService.GetAllCategoriesAsync(query);
            return Ok(new ApiResult(
                data: new
                {
                    categories = result.Categories,
                    totalCount = result.TotalCount,
                    pageNumber = result.PageNumber,
                    pageSize = result.PageSize,
                    totalPages = result.TotalPages,
                    hasPreviousPage = result.HasPreviousPage,
                    hasNextPage = result.HasNextPage
                },
                count: result.TotalCount));
        }

        [HttpGet("categories/{categoryCode}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetCategoryByCode(string categoryCode)
        {
            var category = await _categoryService.GetCategoryByCodeAsync(categoryCode);
            return Ok(new ApiResult(data: category));
        }

        [HttpPut("categories/{categoryCode}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdateCategory(
            string categoryCode, [FromBody] UpdateCategoryDto dto)
        {
            var category = await _categoryService.UpdateCategoryAsync(categoryCode, dto);
            return Ok(new ApiResult(data: category, message: "Category updated successfully"));
        }

        [HttpPost("categories/{categoryCode}/toggle-activation")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ToggleCategoryActivation(string categoryCode)
        {
            var category = await _categoryService.ToggleActivationAsync(categoryCode);
            var msg = category.Status == "Active"
                ? "Category activated successfully"
                : "Category deactivated successfully";
            return Ok(new ApiResult(data: category, message: msg));
        }

        [HttpDelete("categories/{categoryCode}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteCategory(string categoryCode)
        {
            await _categoryService.DeleteCategoryAsync(categoryCode);
            return Ok(new ApiResult(message: "Category deleted successfully"));
        }


        
        [HttpGet("products/{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetProductById(int id)
        {
            var product = await _productService.GetProductByIdAsync(id);
            return Ok(new ApiResult(data: product));
        }

        [HttpGet("products/{code:regex(^[[A-Za-z0-9-]]{{3,}}$)}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetProductByCode(string code)
        {
            var product = await _productService.GetProductByCodeAsync(code);
            return Ok(new ApiResult(data: product));
        }

        [HttpPost("products")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CreateProduct([FromBody] ProductCreateDto dto)
        {
            var product = await _productService.CreateProductAsync(dto);
            return StatusCode(201, new ApiResult(data: product, message: "Product created successfully"));
        }

        [HttpPut("products/{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateProduct(int id, [FromBody] ProductUpdateDto dto)
        {
            var product = await _productService.AdminUpdateProductAsync(id, dto);
            return Ok(new ApiResult(data: product, message: "Product updated successfully"));
        }

        [HttpDelete("products/{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            await _productService.AdminDeleteProductAsync(id);
            return Ok(new ApiResult(message: "Product deleted successfully"));
        }

        [HttpPatch("products/{id:int}/stock")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateProductStock(int id, [FromBody] UpdateStockDto dto)
        {
            await _productService.AdminUpdateStockAsync(id, dto.Quantity);
            return Ok(new ApiResult(message: "Stock updated successfully"));
        }

        [HttpPost("products/{id:int}/toggle-status")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ToggleProductStatus(int id)
        {
            var product = await _productService.AdminToggleProductStatusAsync(id);
            var msg = product.IsActive ? "Product activated successfully" : "Product deactivated successfully";
            return Ok(new ApiResult(data: product, message: msg));
        }


        [HttpGet("reports/sales")]
        public async Task<IActionResult> GetSalesReport(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate,
            [FromQuery] string? vendorCode = null)
        {
            if (startDate == DateTime.MinValue || endDate == DateTime.MinValue)
                return BadRequest(new ApiResponse(400, "Start date and end date are required"));

            int? vendorId = null;
            if (!string.IsNullOrEmpty(vendorCode))
                vendorId = await _codeLookup.ResolveVendorIdAsync(vendorCode);

            var report = await _reportService.GetSalesReportAsync(startDate, endDate, vendorId);
            return Ok(new ApiResult(data: report));
        }

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

        [HttpGet("reports/vendor-performance/{vendorCode}")]
        public async Task<IActionResult> GetVendorPerformance(string vendorCode)
        {
            var vendorId = await _codeLookup.ResolveVendorIdAsync(vendorCode);
            var report = await _reportService.GetVendorPerformanceAsync(vendorId);
            return Ok(new ApiResult(data: report));
        }

        [HttpGet("reports/customer-insights")]
        public async Task<IActionResult> GetCustomerInsights()
        {
            var report = await _reportService.GetCustomerInsightsAsync();
            return Ok(new ApiResult(data: report));
        }

        [HttpGet("reports/low-stock")]
        public async Task<IActionResult> GetLowStockProducts([FromQuery] int threshold = 10)
        {
            var report = await _reportService.GetLowStockProductsAsync(threshold);
            return Ok(new ApiResult(data: report));
        }

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

        [HttpGet("reports/top-products")]
        public async Task<IActionResult> GetTopProductsReport(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate,
            [FromQuery] int take = 10)
        {
            if (startDate == DateTime.MinValue || endDate == DateTime.MinValue)
                return BadRequest(new ApiResponse(400, "Start date and end date are required"));

            var report = await _reportService.GetTopProductsAsync(startDate, endDate, take);
            return Ok(new ApiResult(data: report));
        }

        [HttpGet("reports/order-status-summary")]
        public async Task<IActionResult> GetOrderStatusSummary()
        {
            var report = await _reportService.GetOrderStatusSummaryAsync();
            return Ok(new ApiResult(data: report));
        }

        [HttpGet("reports/user-trends")]
        public async Task<IActionResult> GetUserTrends()
        {
            var report = await _reportService.GetUserTrendsAsync();
            return Ok(new ApiResult(data: report));
        }
    }
}
