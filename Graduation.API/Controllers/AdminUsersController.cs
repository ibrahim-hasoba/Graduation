using Graduation.BLL.Services.Implementations;
using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Graduation.BLL.DTOs.Admin;
using System.Text;

namespace Graduation.API.Controllers
{
    [Route("api/admin/users")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminUsersController : BaseController
    {
        private readonly DatabaseContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly ICodeLookupService _codeLookup;
        private readonly ICodeAssignmentService _codeAssignment;
        private readonly IImageService _imageService;
        private readonly IOrderService _orderService;
        private readonly IActivityLogService _activityLog;

        public AdminUsersController(
            DatabaseContext context,
            UserManager<AppUser> userManager,
            ICodeLookupService codeLookup,
            ICodeAssignmentService codeAssignment,
            IImageService imageService,
            IOrderService orderService,
            IActivityLogService activityLog,
            ILanguageService lang)
            : base(lang)
        {
            _context = context;
            _userManager = userManager;
            _codeLookup = codeLookup;
            _codeAssignment = codeAssignment;
            _imageService = imageService;
            _orderService = orderService;
            _activityLog = activityLog;
        }

        [HttpGet("{userCode}")]
        public async Task<IActionResult> GetUser(string userCode)
        {
            var userId = await _codeLookup.ResolveUserIdAsync(userCode);
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) throw new Graduation.BLL.Errors.NotFoundException(Lang.GetMessage(LangKeys.User.NotFound));

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

        [HttpGet]
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

        [HttpDelete("{userCode}")]
        public async Task<IActionResult> DeleteUser(string userCode)
        {
            var requestingAdminId = GetRequiredUserId();

            var userId = await _codeLookup.ResolveUserIdAsync(userCode);

            if (requestingAdminId == userId)
                throw new Graduation.BLL.Errors.BadRequestException(Lang.GetMessage(LangKeys.User.CannotDeleteSelf));

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                throw new Graduation.BLL.Errors.NotFoundException(Lang.GetMessage(LangKeys.User.NotFound));

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
                throw new Graduation.BLL.Errors.BadRequestException(
                    string.Join(", ", result.Errors.Select(e => e.Description)));

            await _activityLog.LogAsync(GetRequiredUserId(), "Delete", "User", user.Code, $"Deleted user {user.Email}");
            return OkResult(message: Lang.GetMessage(LangKeys.User.Deleted));
        }

        [HttpPost("{userCode}/toggle-lock")]
        public async Task<IActionResult> ToggleUserLock(string userCode)
        {
            var userId = await _codeLookup.ResolveUserIdAsync(userCode);
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) throw new Graduation.BLL.Errors.NotFoundException(Lang.GetMessage(LangKeys.User.NotFound));

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

        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] AdminCreateUserDto dto)
        {
            var existingUser = await _userManager.FindByEmailAsync(dto.Email);
            if (existingUser != null)
                throw new Graduation.BLL.Errors.BadRequestException(Lang.GetMessage(LangKeys.Auth.EmailAlreadyExists));

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
                throw new Graduation.BLL.Errors.BadRequestException(
                    string.Join(", ", result.Errors.Select(e => e.Description)));

            if (!await _context.Roles.AnyAsync(r => r.Name == dto.Role))
                throw new Graduation.BLL.Errors.NotFoundException(Lang.GetMessage(LangKeys.Role.NotFound));

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

        [HttpPut("{userCode}")]
        public async Task<IActionResult> UpdateUser(string userCode, [FromBody] UpdateUserDto dto)
        {
            var userId = await _codeLookup.ResolveUserIdAsync(userCode);
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) throw new Graduation.BLL.Errors.NotFoundException(Lang.GetMessage(LangKeys.User.NotFound));

            user.FirstName = dto.FirstName ?? user.FirstName;
            user.LastName = dto.LastName ?? user.LastName;
            user.PhoneNumber = dto.PhoneNumber ?? user.PhoneNumber;
            user.UpdatedAt = DateTime.UtcNow;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                throw new Graduation.BLL.Errors.BadRequestException(result.Errors.First().Description);

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

        [HttpPost("{userCode}/reset-password")]
        public async Task<IActionResult> ResetUserPassword(string userCode, [FromBody] AdminResetPasswordDto dto)
        {
            var userId = await _codeLookup.ResolveUserIdAsync(userCode);
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) throw new Graduation.BLL.Errors.NotFoundException(Lang.GetMessage(LangKeys.User.NotFound));

            var isTargetAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            if (isTargetAdmin)
                throw new Graduation.BLL.Errors.BadRequestException(Lang.GetMessage(LangKeys.User.AdminPasswordReset));

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, dto.NewPassword);
            if (!result.Succeeded)
                throw new Graduation.BLL.Errors.BadRequestException(string.Join(", ", result.Errors.Select(e => e.Description)));

            await _userManager.UpdateSecurityStampAsync(user);
            await _activityLog.LogAsync(GetRequiredUserId(), "ResetPassword", "User", user.Code, $"Reset password for user {user.Email}");
            return OkResult(message: Lang.GetMessage(LangKeys.User.PasswordReset));
        }

        [HttpGet("export")]
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
    }
}
