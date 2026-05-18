using Graduation.BLL.Services.Implementations;
using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs.Vendor;

namespace Graduation.API.Controllers
{
    [Route("api/admin/vendors")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminVendorsController : BaseController
    {
        private readonly IVendorService _vendorService;
        private readonly ICodeLookupService _codeLookup;
        private readonly ICodeAssignmentService _codeAssignment;
        private readonly DatabaseContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IActivityLogService _activityLog;

        public AdminVendorsController(
            IVendorService vendorService,
            ICodeLookupService codeLookup,
            ICodeAssignmentService codeAssignment,
            DatabaseContext context,
            UserManager<AppUser> userManager,
            IActivityLogService activityLog,
            ILanguageService lang)
            : base(lang)
        {
            _vendorService = vendorService;
            _codeLookup = codeLookup;
            _codeAssignment = codeAssignment;
            _context = context;
            _userManager = userManager;
            _activityLog = activityLog;
        }
        /// <summary>Gets a paginated list of all vendors with optional filtering by approval status, activity, and search.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet]
        public async Task<IActionResult> GetAllVendors(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] bool? isApproved = null,
            [FromQuery] bool? isActive = null,
            [FromQuery] string? search = null,
            [FromQuery] string? approvalStatus = null)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var query = _context.Vendors
                .Include(v => v.User)
                .Include(v => v.Products)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(approvalStatus))
            {
                if (Enum.TryParse<VendorApprovalStatus>(approvalStatus, ignoreCase: true, out var parsedStatus))
                    query = query.Where(v => v.ApprovalStatus == parsedStatus);
            }
            else if (isApproved.HasValue)
            {
                var targetStatus = isApproved.Value
                    ? VendorApprovalStatus.Approved
                    : VendorApprovalStatus.Pending;
                query = query.Where(v => v.ApprovalStatus == targetStatus);
            }

            if (isActive.HasValue)
                query = query.Where(v => v.IsActive == isActive.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                query = query.Where(v =>
                    v.StoreName.ToLower().Contains(s) ||
                    v.StoreNameAr.Contains(search));
            }

            var totalCount = await query.CountAsync();

            var vendors = await query
                .OrderByDescending(v => v.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return OkResult(
                data: PaginatedResponse(vendors.Select(MapToDto), totalCount, pageNumber, pageSize));
        }
        /// <summary>Gets a single vendor's full details by their vendor code.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpGet("{vendorCode}")]
        public async Task<IActionResult> GetVendor(string vendorCode)
        {
            var id = await _codeLookup.ResolveVendorIdAsync(vendorCode);
            var vendor = await _context.Vendors
                .Include(v => v.User)
                .Include(v => v.Products)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (vendor == null)
                throw new Shared.Errors.NotFoundException(Lang.GetMessage(LangKeys.Vendor.NotFound));

            return OkResult(data: MapToDto(vendor));
        }
        /// <summary>Creates a new vendor account linked to an existing user, with optional immediate approval.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpPost]
        public async Task<IActionResult> CreateVendor([FromBody] AdminCreateVendorDto dto)
        {
            var userId = await _codeLookup.ResolveUserIdAsync(dto.UserCode);

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                throw new Shared.Errors.NotFoundException(Lang.GetMessage(LangKeys.User.NotFound));

            var exists = await _context.Vendors.AnyAsync(v => v.UserId == userId);
            if (exists)
                throw new Shared.Errors.ConflictException(Lang.GetMessage(LangKeys.Vendor.AlreadyExists));

            var nameExists = await _context.Vendors
                .AnyAsync(v => v.StoreName.ToLower() == dto.StoreName.ToLower());
            if (nameExists)
                throw new Shared.Errors.ConflictException(Lang.GetMessage(LangKeys.Vendor.NameExists));

            var initialStatus = dto.IsApproved
                ? VendorApprovalStatus.Approved
                : VendorApprovalStatus.Pending;

            var vendor = new Vendor
            {
                UserId = userId,
                StoreName = dto.StoreName,
                StoreNameAr = dto.StoreNameAr,
                StoreDescription = dto.StoreDescription,
                StoreDescriptionAr = dto.StoreDescriptionAr,
                PhoneNumber = dto.PhoneNumber,
                Address = dto.Address,
                City = dto.City,
                LogoUrl = dto.LogoUrl,
                BannerUrl = dto.BannerUrl,
                ApprovalStatus = initialStatus,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
            };

            _context.Vendors.Add(vendor);
            await _context.SaveChangesAsync();

            await _codeAssignment.AssignVendorCodeAsync(vendor);

            if (dto.IsApproved && !await _userManager.IsInRoleAsync(user, "Vendor"))
                await _userManager.AddToRoleAsync(user, "Vendor");

            await _activityLog.LogAsync(GetRequiredUserId(), "Create", "Vendor", vendor.Code, $"Created vendor {vendor.StoreName}");

            vendor = await _context.Vendors
                .Include(v => v.User)
                .Include(v => v.Products)
                .FirstAsync(v => v.Id == vendor.Id);

            return CreatedResult(
                data: MapToDto(vendor),
                message: Lang.GetMessage(LangKeys.Vendor.Created));
        }
        /// <summary>Updates a vendor's profile, approval status, and activity status by vendor code.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPut("{vendorCode}")]
        public async Task<IActionResult> UpdateVendor(
            string vendorCode, [FromBody] AdminUpdateVendorDto dto)
        {
            var id = await _codeLookup.ResolveVendorIdAsync(vendorCode);

            var vendor = await _context.Vendors
                .Include(v => v.User)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (vendor == null)
                throw new Shared.Errors.NotFoundException(Lang.GetMessage(LangKeys.Vendor.NotFound));

            if (!string.IsNullOrWhiteSpace(dto.StoreName) &&
                !dto.StoreName.Equals(vendor.StoreName, StringComparison.OrdinalIgnoreCase))
            {
                var nameExists = await _context.Vendors
                    .AnyAsync(v => v.Id != id &&
                                   v.StoreName.ToLower() == dto.StoreName.ToLower());
                if (nameExists)
                    throw new Shared.Errors.ConflictException(Lang.GetMessage(LangKeys.Vendor.NameExists));

                vendor.StoreName = dto.StoreName;
            }

            if (!string.IsNullOrWhiteSpace(dto.StoreNameAr))
                vendor.StoreNameAr = dto.StoreNameAr;
            if (!string.IsNullOrWhiteSpace(dto.StoreDescription))
                vendor.StoreDescription = dto.StoreDescription;
            if (!string.IsNullOrWhiteSpace(dto.StoreDescriptionAr))
                vendor.StoreDescriptionAr = dto.StoreDescriptionAr;
            if (!string.IsNullOrWhiteSpace(dto.PhoneNumber))
                vendor.PhoneNumber = dto.PhoneNumber;
            if (!string.IsNullOrWhiteSpace(dto.Address))
                vendor.Address = dto.Address;
            if (!string.IsNullOrWhiteSpace(dto.City))
                vendor.City = dto.City;
            if (dto.Latitude.HasValue)
                vendor.Latitude = dto.Latitude;

            if (dto.Longitude.HasValue)
                vendor.Longitude = dto.Longitude;
            if (dto.LogoUrl != null) vendor.LogoUrl = dto.LogoUrl;
            if (dto.BannerUrl != null) vendor.BannerUrl = dto.BannerUrl;

            if (dto.IsApproved.HasValue)
            {
                var newApproval = dto.IsApproved.Value
                    ? VendorApprovalStatus.Approved
                    : VendorApprovalStatus.Pending;

                if (newApproval != vendor.ApprovalStatus)
                {
                    vendor.ApprovalStatus = newApproval;
                    if (newApproval == VendorApprovalStatus.Approved)
                        vendor.RejectionReason = null;

                    var appUser = await _userManager.FindByIdAsync(vendor.UserId);
                    if (appUser != null)
                    {
                        if (dto.IsApproved.Value && !await _userManager.IsInRoleAsync(appUser, "Vendor"))
                            await _userManager.AddToRoleAsync(appUser, "Vendor");
                        else if (!dto.IsApproved.Value && await _userManager.IsInRoleAsync(appUser, "Vendor"))
                            await _userManager.RemoveFromRoleAsync(appUser, "Vendor");
                    }
                }
            }

            if (dto.IsActive.HasValue)
                vendor.IsActive = dto.IsActive.Value;

            vendor.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await _activityLog.LogAsync(GetRequiredUserId(), "Update", "Vendor", vendorCode, $"Updated vendor {vendor.StoreName}");

            vendor = await _context.Vendors
                .Include(v => v.User)
                .Include(v => v.Products)
                .FirstAsync(v => v.Id == id);

            return OkResult(
                data: MapToDto(vendor),
                message: Lang.GetMessage(LangKeys.Vendor.Updated));
        }
        /// <summary>Permanently deletes a vendor and removes the vendor role from the associated user.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpDelete("{vendorCode}")]
        public async Task<IActionResult> DeleteVendor(string vendorCode)
        {
            var id = await _codeLookup.ResolveVendorIdAsync(vendorCode);

            var vendor = await _context.Vendors
                .Include(v => v.User)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (vendor == null)
                throw new Shared.Errors.NotFoundException(Lang.GetMessage(LangKeys.Vendor.NotFound));

            var appUser = await _userManager.FindByIdAsync(vendor.UserId);
            if (appUser != null && await _userManager.IsInRoleAsync(appUser, "Vendor"))
                await _userManager.RemoveFromRoleAsync(appUser, "Vendor");

            await _activityLog.LogAsync(GetRequiredUserId(), "Delete", "Vendor", vendorCode, $"Deleted vendor {vendor.StoreName}");

            _context.Vendors.Remove(vendor);
            await _context.SaveChangesAsync();

            return OkResult(message: Lang.GetMessage(LangKeys.Vendor.Deleted));
        }
        /// <summary>Approves a pending vendor application and grants the vendor role.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPost("{vendorCode}/approve")]
        public async Task<IActionResult> ApproveVendor(string vendorCode)
        {
            var id = await _codeLookup.ResolveVendorIdAsync(vendorCode);
            var result = await _vendorService.ApproveVendorAsync(id, isApproved: true);
            await _activityLog.LogAsync(GetRequiredUserId(), "Approve", "Vendor", vendorCode, $"Approved vendor {result.StoreName}");
            return OkResult(data: result, message: Lang.GetMessage(LangKeys.Vendor.Approved));
        }
        /// <summary>Rejects a pending vendor application with a required rejection reason.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPost("{vendorCode}/reject")]
        public async Task<IActionResult> RejectVendor(
            string vendorCode, [FromBody] VendorApprovalDto dto)
        {
            if (!dto.IsApproved && string.IsNullOrWhiteSpace(dto.RejectionReason))
                throw new Shared.Errors.BadRequestException(Lang.GetMessage(LangKeys.Vendor.RejectionRequired));

            var id = await _codeLookup.ResolveVendorIdAsync(vendorCode);
            var result = await _vendorService.ApproveVendorAsync(
                id, isApproved: false, rejectionReason: dto.RejectionReason);

            await _activityLog.LogAsync(GetRequiredUserId(), "Reject", "Vendor", vendorCode, $"Rejected vendor {result.StoreName}. Reason: {dto.RejectionReason}");
            return OkResult(data: result, message: Lang.GetMessage(LangKeys.Vendor.Rejected));
        }
        /// <summary>Toggles a vendor's active/inactive status.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPost("{vendorCode}/toggle-status")]
        public async Task<IActionResult> ToggleVendorStatus(string vendorCode)
        {
            var id = await _codeLookup.ResolveVendorIdAsync(vendorCode);
            var result = await _vendorService.ToggleVendorStatusAsync(id);
            var action = result.IsActive ? "Activate" : "Deactivate";
            await _activityLog.LogAsync(GetRequiredUserId(), action, "Vendor", vendorCode, $"{action}d vendor {result.StoreName}");
            var msg = result.IsActive ? Lang.GetMessage(LangKeys.Vendor.Activated) : Lang.GetMessage(LangKeys.Vendor.Deactivated);
            return OkResult(data: result, message: msg);
        }

        private static object MapToDto(Vendor v) => new
        {
            vendorCode = v.Code,
            ownerCode = v.User?.Code,
            ownerEmail = v.User?.Email,
            ownerName = v.User != null
                                   ? $"{v.User.FirstName} {v.User.LastName}".Trim()
                                   : null,
            storeName = v.StoreName,
            storeNameAr = v.StoreNameAr,
            storeDescription = v.StoreDescription,
            storeDescriptionAr = v.StoreDescriptionAr,
            logoUrl = v.LogoUrl,
            bannerUrl = v.BannerUrl,
            phoneNumber = v.PhoneNumber,
            address = v.Address,
            city = v.City,
            latitude = v.Latitude,
            longitude = v.Longitude,
            approvalStatus = v.ApprovalStatus.ToString(),
            approvalStatusId = (int)v.ApprovalStatus,
            rejectionReason = v.RejectionReason,
            isApproved = v.IsApproved,
            isActive = v.IsActive,
            totalProducts = v.Products?.Count ?? 0,
            createdAt = v.CreatedAt,
            updatedAt = v.UpdatedAt
        };
    }
}