using Graduation.BLL.Services.Implementations;
using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs.Admin;
using Shared.DTOs.Category;
using Shared.DTOs.Product;
using Graduation.API.Extensions;
using Microsoft.AspNetCore.Identity;
using System.Text;

namespace Graduation.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminController : BaseController
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
        private readonly IReviewReportService _reviewReportService;
        private readonly IActivityLogService _activityLog;

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
            IOrderService orderService,
            IReviewReportService reviewReportService,
            IActivityLogService activityLog,
            ILanguageService lang)
            : base(lang)
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
            _reviewReportService = reviewReportService;
            _activityLog = activityLog;
        }
        /// <summary>Gets aggregate dashboard statistics for the admin panel.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet("dashboard/stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            var stats = await _adminService.GetDashboardStatsAsync();
            return OkResult(data: stats);
        }
        /// <summary>Gets the most recent admin activities.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet("dashboard/activities")]
        public async Task<IActionResult> GetRecentActivities([FromQuery] int count = 10)
        {
            var activities = await _activityLog.GetRecentActivitiesAsync(count);
            return OkResult(data: activities);
        }
        /// <summary>Gets the top-selling products for the admin dashboard.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet("dashboard/top-products")]
        public async Task<IActionResult> GetTopProducts([FromQuery] int count = 10)
        {
            var products = await _adminService.GetTopProductsAsync(count);
            return OkResult(data: products);
        }
        /// <summary>Gets the top-performing vendors for the admin dashboard.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet("dashboard/top-vendors")]
        public async Task<IActionResult> GetTopVendors([FromQuery] int count = 10)
        {
            var vendors = await _adminService.GetTopVendorsAsync(count);
            return OkResult(data: vendors);
        }
        /// <summary>Gets sales chart data for the admin dashboard.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet("dashboard/sales-chart")]
        public async Task<IActionResult> GetSalesChart()
        {
            var chartData = await _adminService.GetSalesChartDataAsync();
            return OkResult(data: chartData);
        }
        /// <summary>Gets user statistics for the admin dashboard.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet("dashboard/user-stats")]
        public async Task<IActionResult> GetUserStats()
        {
            var stats = await _adminService.GetUserStatsAsync();
            return OkResult(data: stats);
        }
        /// <summary>Gets a single user's details by their user code.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpGet("users/{userCode}")]
        public async Task<IActionResult> GetUser(string userCode)
        {
            var userId = await _codeLookup.ResolveUserIdAsync(userCode);
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) throw new Shared.Errors.NotFoundException(Lang.GetMessage(LangKeys.User.NotFound));

            var fullProfilePictureUrl = _imageService.GetFullImageUrl(user.ProfilePictureUrl!);

            return OkResult(data: new
            {
                id = user.Id,
                userCode = user.Code,
                email = user.Email,
                firstName = user.FirstName,
                lastName = user.LastName,
                emailConfirmed = user.EmailConfirmed,
                phoneNumber = user.PhoneNumber,
                profilePicture = fullProfilePictureUrl,
                createdAt = user.CreatedAt,
                isLocked = user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow,
            });
        }
        /// <summary>Gets a paginated list of all users, optionally filtered by role.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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

            return OkResult(
                data: PaginatedResponse(users, totalCount, pageNumber, pageSize));
        }
        /// <summary>Permanently deletes a user by user code. Admin cannot delete themselves.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpDelete("users/{userCode}")]
        public async Task<IActionResult> DeleteUser(string userCode)
        {
            var requestingAdminId = GetRequiredUserId();

            var userId = await _codeLookup.ResolveUserIdAsync(userCode);

            if (requestingAdminId == userId)
                throw new Shared.Errors.BadRequestException(Lang.GetMessage(LangKeys.User.CannotDeleteSelf));

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                throw new Shared.Errors.NotFoundException(Lang.GetMessage(LangKeys.User.NotFound));

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
                throw new Shared.Errors.BadRequestException(
                    string.Join(", ", result.Errors.Select(e => e.Description)));

            await _activityLog.LogAsync(GetRequiredUserId(), "Delete", "User", user.Code, $"Deleted user {user.Email}");
            return OkResult(message: Lang.GetMessage(LangKeys.User.Deleted));
        }
        /// <summary>Toggles a user's account lockout status between locked and unlocked.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPost("users/{userCode}/toggle-lock")]
        public async Task<IActionResult> ToggleUserLock(string userCode)
        {
            var userId = await _codeLookup.ResolveUserIdAsync(userCode);
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) throw new Shared.Errors.NotFoundException(Lang.GetMessage(LangKeys.User.NotFound));

            if (user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow)
            {
                await _userManager.SetLockoutEndDateAsync(user, null);
                await _activityLog.LogAsync(GetRequiredUserId(), "Unlock", "User", user.Code, $"Unlocked user {user.Email}");
                return OkResult(data: new { locked = false }, message: Lang.GetMessage(LangKeys.User.Unlocked));
            }
            else
            {
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
                await _activityLog.LogAsync(GetRequiredUserId(), "Lock", "User", user.Code, $"Locked user {user.Email}");
                return OkResult(data: new { locked = true }, message: Lang.GetMessage(LangKeys.User.Locked));
            }
        }
        /// <summary>Creates a new user with a specified role and assigns a user code.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpPost("users")]
        public async Task<IActionResult> CreateUser([FromBody] AdminCreateUserDto dto)
        {
            var existingUser = await _userManager.FindByEmailAsync(dto.Email);
            if (existingUser != null)
                throw new Shared.Errors.BadRequestException(Lang.GetMessage(LangKeys.Auth.EmailAlreadyExists));

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
                throw new Shared.Errors.BadRequestException(
                    string.Join(", ", result.Errors.Select(e => e.Description)));

            if (!await _context.Roles.AnyAsync(r => r.Name == dto.Role))
                throw new Shared.Errors.NotFoundException(Lang.GetMessage(LangKeys.Role.NotFound));

            await _userManager.AddToRoleAsync(user, dto.Role);
            await _codeAssignment.AssignUserCodeAsync(user);

            await _activityLog.LogAsync(GetRequiredUserId(), "Create", "User", user.Code, $"Created user {user.Email} with role {dto.Role}");

            return OkResult(data: new
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
            }, message: Lang.GetMessage(LangKeys.User.Created));
        }
        /// <summary>Updates a user's profile fields by their user code.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPut("users/{userCode}")]
        public async Task<IActionResult> UpdateUser(string userCode, [FromBody] UpdateUserDto dto)
        {
            var userId = await _codeLookup.ResolveUserIdAsync(userCode);
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) throw new Shared.Errors.NotFoundException(Lang.GetMessage(LangKeys.User.NotFound));

            user.FirstName = dto.FirstName ?? user.FirstName;
            user.LastName = dto.LastName ?? user.LastName;
            user.PhoneNumber = dto.PhoneNumber ?? user.PhoneNumber;
            user.UpdatedAt = DateTime.UtcNow;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                throw new Shared.Errors.BadRequestException(result.Errors.First().Description);

            await _activityLog.LogAsync(GetRequiredUserId(), "Update", "User", user.Code, $"Updated user {user.Email}");

            return OkResult(data: new
            {
                userCode = user.Code,
                email = user.Email,
                firstName = user.FirstName,
                lastName = user.LastName,
                phoneNumber = user.PhoneNumber,
                updatedAt = user.UpdatedAt,
                isLocked = user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow
            }, message: Lang.GetMessage(LangKeys.User.Updated));
        }
        /// <summary>Resets a non-admin user's password and invalidates existing sessions.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPost("users/{userCode}/reset-password")]
        public async Task<IActionResult> ResetUserPassword(string userCode, [FromBody] AdminResetPasswordDto dto)
        {
            var userId = await _codeLookup.ResolveUserIdAsync(userCode);
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) throw new Shared.Errors.NotFoundException(Lang.GetMessage(LangKeys.User.NotFound));

            var isTargetAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            if (isTargetAdmin)
                throw new Shared.Errors.BadRequestException(Lang.GetMessage(LangKeys.User.AdminPasswordReset));

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, dto.NewPassword);
            if (!result.Succeeded)
                throw new Shared.Errors.BadRequestException(string.Join(", ", result.Errors.Select(e => e.Description)));

            await _userManager.UpdateSecurityStampAsync(user);
            await _activityLog.LogAsync(GetRequiredUserId(), "ResetPassword", "User", user.Code, $"Reset password for user {user.Email}");
            return OkResult(message: Lang.GetMessage(LangKeys.User.PasswordReset));
        }
        /// <summary>Exports users to a CSV file, optionally filtered by role.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
        /// <summary>Gets a paginated list of all orders with optional status filter.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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

            return OkResult(
                data: PaginatedResponse(orders, totalCount, pageNumber, pageSize));
        }
        /// <summary>Gets a high-level system summary including users, vendors, products, orders, and revenue.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
        /// <summary>Creates a new product category.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpPost("categories")]
        public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryDto dto)
        {
            var category = await _categoryService.CreateCategoryAsync(dto);
            await _activityLog.LogAsync(GetRequiredUserId(), "Create", "Category", category.Code, $"Created category {category.NameEn}");
            return CreatedAtAction(
                nameof(GetCategoryByCode),
                new { categoryCode = category.Code },
                new Errors.ApiResult(data: category));
        }
        /// <summary>Gets a paginated list of all categories with optional query filters.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet("categories")]
        public async Task<IActionResult> GetAllCategories([FromQuery] CategoryQueryDto query)
        {
            var result = await _categoryService.GetAllCategoriesAsync(query);
            return OkResult(
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
                count: result.TotalCount);
        }
        /// <summary>Gets a single category by its code.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpGet("categories/{categoryCode}")]
        public async Task<IActionResult> GetCategoryByCode(string categoryCode)
        {
            var category = await _categoryService.GetCategoryByCodeAsync(categoryCode);
            return OkResult(data: category);
        }
        /// <summary>Updates an existing category by its code.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPut("categories/{categoryCode}")]
        public async Task<IActionResult> UpdateCategory(
            string categoryCode, [FromBody] UpdateCategoryDto dto)
        {
            var category = await _categoryService.UpdateCategoryAsync(categoryCode, dto);
            await _activityLog.LogAsync(GetRequiredUserId(), "Update", "Category", categoryCode, $"Updated category {category.NameEn}");
            return OkResult(data: category, message: Lang.GetMessage(LangKeys.Category.Updated));
        }
        /// <summary>Toggles a category's active/inactive status.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPost("categories/{categoryCode}/toggle-activation")]
        public async Task<IActionResult> ToggleCategoryActivation(string categoryCode)
        {
            var category = await _categoryService.ToggleActivationAsync(categoryCode);
            var action = category.Status == "Active" ? "Activate" : "Deactivate";
            await _activityLog.LogAsync(GetRequiredUserId(), action, "Category", categoryCode, $"{action}d category {category.NameEn}");
            var msg = category.Status == "Active"
                ? Lang.GetMessage(LangKeys.Category.Activated)
                : Lang.GetMessage(LangKeys.Category.Deactivated);
            return OkResult(data: category, message: msg);
        }
        /// <summary>Deletes a category by its code.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpDelete("categories/{categoryCode}")]
        public async Task<IActionResult> DeleteCategory(string categoryCode)
        {
            await _activityLog.LogAsync(GetRequiredUserId(), "Delete", "Category", categoryCode, $"Deleted category {categoryCode}");
            await _categoryService.DeleteCategoryAsync(categoryCode);
            return OkResult(message: Lang.GetMessage(LangKeys.Category.Deleted));
        }
        /// <summary>Gets a paginated list of all products with optional search filters (admin).</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet("products")]
        public async Task<IActionResult> GetAllProducts([FromQuery] ProductSearchDto searchDto)
        {
            var result = await _productService.SearchProductsAsync(searchDto);
            return OkResult(data: result);
        }
        /// <summary>Gets a single product by its numeric ID.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpGet("products/{id:int}")]
        public async Task<IActionResult> GetProductById(int id)
        {
            var product = await _productService.GetProductByIdAsync(id);
            return OkResult(data: product);
        }
        /// <summary>Gets a single product by its alphanumeric code.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpGet("products/{code:regex(^[[A-Za-z0-9-]]{{3,}}$)}")]
        public async Task<IActionResult> GetProductByCode(string code)
        {
            var product = await _productService.GetProductByCodeAsync(code);
            return OkResult(data: product);
        }
        /// <summary>Creates a new product as an admin bypassing vendor ownership.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpPost("products")]
        public async Task<IActionResult> CreateProduct([FromBody] ProductCreateDto dto)
        {
            var product = await _productService.CreateProductAsync(dto);
            await _activityLog.LogAsync(GetRequiredUserId(), "Create", "Product", product.Code, $"Created product {product.NameEn}");
            return StatusCode(201, new Errors.ApiResult(data: product, message: Lang.GetMessage(LangKeys.Product.AdminCreated)));
        }
        /// <summary>Updates any product by ID as an admin.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPut("products/{id:int}")]
        public async Task<IActionResult> UpdateProduct(int id, [FromBody] ProductUpdateDto dto)
        {
            var product = await _productService.AdminUpdateProductAsync(id, dto);
            await _activityLog.LogAsync(GetRequiredUserId(), "Update", "Product", product.Code, $"Updated product {product.NameEn}");
            return OkResult(data: product, message: Lang.GetMessage(LangKeys.Product.AdminUpdated));
        }
        /// <summary>Deletes any product by ID as an admin.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpDelete("products/{id:int}")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            await _activityLog.LogAsync(GetRequiredUserId(), "Delete", "Product", id.ToString(), $"Deleted product #{id}");
            await _productService.AdminDeleteProductAsync(id);
            return OkResult(message: Lang.GetMessage(LangKeys.Product.AdminDeleted));
        }
        /// <summary>Updates the stock quantity of a product.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPatch("products/{id:int}/stock")]
        public async Task<IActionResult> UpdateProductStock(int id, [FromBody] UpdateStockDto dto)
        {
            await _activityLog.LogAsync(GetRequiredUserId(), "UpdateStock", "Product", id.ToString(), $"Updated stock for product #{id} to {dto.Quantity}");
            await _productService.AdminUpdateStockAsync(id, dto.Quantity);
            return OkResult(message: Lang.GetMessage(LangKeys.Product.StockAdminUpdated));
        }
        /// <summary>Toggles a product's active/inactive status.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPost("products/{id:int}/toggle-status")]
        public async Task<IActionResult> ToggleProductStatus(int id)
        {
            var product = await _productService.AdminToggleProductStatusAsync(id);
            var action = product.IsActive ? "Activate" : "Deactivate";
            await _activityLog.LogAsync(GetRequiredUserId(), action, "Product", product.Code, $"{action}d product {product.NameEn}");
            var msg = product.IsActive ? Lang.GetMessage(LangKeys.Product.Activated) : Lang.GetMessage(LangKeys.Product.Deactivated);
            return OkResult(data: product, message: msg);
        }
        /// <summary>Gets a sales report for a specified date range, optionally filtered by vendor.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet("reports/sales")]
        public async Task<IActionResult> GetSalesReport(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate,
            [FromQuery] string? vendorCode = null)
        {
            if (startDate == DateTime.MinValue || endDate == DateTime.MinValue)
                return BadRequest(new Shared.Errors.ApiResponse(400, Lang.GetMessage(LangKeys.Report.DateRangeRequired)));

            int? vendorId = null;
            if (!string.IsNullOrEmpty(vendorCode))
                vendorId = await _codeLookup.ResolveVendorIdAsync(vendorCode);

            var report = await _reportService.GetSalesReportAsync(startDate, endDate, vendorId);
            return OkResult(data: report);
        }
        /// <summary>Gets sales data grouped by category within a date range.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet("reports/sales-by-category")]
        public async Task<IActionResult> GetSalesByCategory(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate)
        {
            if (startDate == DateTime.MinValue || endDate == DateTime.MinValue)
                return BadRequest(new Shared.Errors.ApiResponse(400, Lang.GetMessage(LangKeys.Report.DateRangeRequired)));

            var report = await _reportService.GetSalesByCategoryAsync(startDate, endDate);
            return OkResult(data: report);
        }
        /// <summary>Gets a performance report for a specific vendor.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpGet("reports/vendor-performance/{vendorCode}")]
        public async Task<IActionResult> GetVendorPerformance(string vendorCode)
        {
            var vendorId = await _codeLookup.ResolveVendorIdAsync(vendorCode);
            var report = await _reportService.GetVendorPerformanceAsync(vendorId);
            return OkResult(data: report);
        }
        /// <summary>Gets customer insights and analytics.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet("reports/customer-insights")]
        public async Task<IActionResult> GetCustomerInsights()
        {
            var report = await _reportService.GetCustomerInsightsAsync();
            return OkResult(data: report);
        }
        /// <summary>Gets products with stock below a specified threshold.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet("reports/low-stock")]
        public async Task<IActionResult> GetLowStockProducts([FromQuery] int threshold = 10)
        {
            var report = await _reportService.GetLowStockProductsAsync(threshold);
            return OkResult(data: report);
        }
        /// <summary>Gets revenue data grouped by vendor within a date range.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet("reports/revenue-by-vendor")]
        public async Task<IActionResult> GetRevenueByVendor(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate,
            [FromQuery] int take = 10)
        {
            if (startDate == DateTime.MinValue || endDate == DateTime.MinValue)
                return BadRequest(new Shared.Errors.ApiResponse(400, Lang.GetMessage(LangKeys.Report.DateRangeRequired)));

            var report = await _reportService.GetRevenueByVendorAsync(startDate, endDate, take);
            return OkResult(data: report);
        }
        /// <summary>Gets the top-selling products within a date range.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet("reports/top-products")]
        public async Task<IActionResult> GetTopProductsReport(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate,
            [FromQuery] int take = 10)
        {
            if (startDate == DateTime.MinValue || endDate == DateTime.MinValue)
                return BadRequest(new Shared.Errors.ApiResponse(400, Lang.GetMessage(LangKeys.Report.DateRangeRequired)));

            var report = await _reportService.GetTopProductsAsync(startDate, endDate, take);
            return OkResult(data: report);
        }
        /// <summary>Gets a summary of orders grouped by their current status.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet("reports/order-status-summary")]
        public async Task<IActionResult> GetOrderStatusSummary()
        {
            var report = await _reportService.GetOrderStatusSummaryAsync();
            return OkResult(data: report);
        }
        /// <summary>Gets user registration and growth trends.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet("reports/user-trends")]
        public async Task<IActionResult> GetUserTrends()
        {
            var report = await _reportService.GetUserTrendsAsync();
            return OkResult(data: report);
        }
        /// <summary>Gets a paginated list of pending review reports.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet("review-reports")]
        public async Task<IActionResult> GetPendingReviewReports(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _reviewReportService.GetPendingReportsAsync(pageNumber, pageSize);
            return OkResult(data: result, count: result.TotalCount);
        }
        /// <summary>Gets a single review report by its ID.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpGet("review-reports/{reportId}")]
        public async Task<IActionResult> GetReviewReport(int reportId)
        {
            var report = await _reviewReportService.GetReportByIdAsync(reportId);
            if (report == null)
                throw new Shared.Errors.NotFoundException(Lang.GetMessage(LangKeys.Review.NotFoundSimple), reportId);
            return OkResult(data: report);
        }
        /// <summary>Approves a review report and takes action on the reported review.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPost("review-reports/{reportId}/approve")]
        public async Task<IActionResult> ApproveReviewReport(int reportId)
        {
            var adminId = GetRequiredUserId();
            var result = await _reviewReportService.ApproveReportAsync(reportId, adminId);
            if (!result)
                throw new Shared.Errors.BadRequestException(Lang.GetMessage(LangKeys.Report.NotFoundOrResolved));
            await _activityLog.LogAsync(adminId, "Approve", "Review", reportId.ToString(), $"Approved review report #{reportId}");
            return OkResult(message: Lang.GetMessage(LangKeys.Report.Approved));
        }
        /// <summary>Dismisses a review report without taking further action.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPost("review-reports/{reportId}/dismiss")]
        public async Task<IActionResult> DismissReviewReport(int reportId)
        {
            var adminId = GetRequiredUserId();
            var result = await _reviewReportService.DismissReportAsync(reportId, adminId);
            if (!result)
                throw new Shared.Errors.BadRequestException(Lang.GetMessage(LangKeys.Report.NotFoundOrResolved));
            await _activityLog.LogAsync(adminId, "Dismiss", "Review", reportId.ToString(), $"Dismissed review report #{reportId}");
            return OkResult(message: Lang.GetMessage(LangKeys.Report.Dismissed));
        }
        /// <summary>Deletes the reviewed content associated with a review report.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpDelete("review-reports/{reportId}/review")]
        public async Task<IActionResult> DeleteReviewFromReport(int reportId)
        {
            var adminId = GetRequiredUserId();
            var result = await _reviewReportService.DeleteReviewFromReportAsync(reportId, adminId);
            if (!result)
                throw new Shared.Errors.NotFoundException(Lang.GetMessage(LangKeys.Review.NotFoundSimple), reportId);
            await _activityLog.LogAsync(adminId, "Delete", "Review", reportId.ToString(), $"Deleted review from report #{reportId}");
            return OkResult(message: Lang.GetMessage(LangKeys.Report.ReviewDeleted));
        }
    }
}