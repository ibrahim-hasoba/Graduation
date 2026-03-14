using Graduation.BLL.Services.Implementations;
using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs.Vendor;
using Shared.Errors;

namespace Graduation.API.Controllers
{

    [Route("api/admin/vendors")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminVendorsController : ControllerBase
    {
        private readonly IVendorService _vendorService;
        private readonly ICodeLookupService _codeLookup;
        private readonly ICodeAssignmentService _codeAssignment;
        private readonly DatabaseContext _context;
        private readonly UserManager<AppUser> _userManager;

        public AdminVendorsController(
            IVendorService vendorService,
            ICodeLookupService codeLookup,
            ICodeAssignmentService codeAssignment,
            DatabaseContext context,
            UserManager<AppUser> userManager)
        {
            _vendorService = vendorService;
            _codeLookup = codeLookup;
            _codeAssignment = codeAssignment;
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllVendors(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] bool? isApproved = null,
            [FromQuery] bool? isActive = null,
            [FromQuery] string? search = null)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var query = _context.Vendors
                .Include(v => v.User)
                .Include(v => v.Products)
                .AsQueryable();

            if (isApproved.HasValue)
                query = query.Where(v => v.IsApproved == isApproved.Value);

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

            return Ok(new ApiResult(data: new
            {
                vendors = vendors.Select(MapToDto),
                totalCount,
                pageNumber,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            }));
        }

        [HttpGet("{vendorCode}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetVendor(string vendorCode)
        {
            var id = await _codeLookup.ResolveVendorIdAsync(vendorCode);
            var vendor = await _context.Vendors
                .Include(v => v.User)
                .Include(v => v.Products)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (vendor == null)
                throw new NotFoundException("Vendor not found");

            return Ok(new ApiResult(data: MapToDto(vendor)));
        }


        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateVendor([FromBody] AdminCreateVendorDto dto)
        {
            var userId = await _codeLookup.ResolveUserIdAsync(dto.UserId);

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                throw new NotFoundException("User not found");

            var exists = await _context.Vendors.AnyAsync(v => v.UserId == userId);
            if (exists)
                throw new ConflictException("This user already has a vendor account");

            var nameExists = await _context.Vendors
                .AnyAsync(v => v.StoreName.ToLower() == dto.StoreName.ToLower());
            if (nameExists)
                throw new ConflictException("A vendor with this store name already exists");

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
                Governorate = dto.Governorate,
                LogoUrl = dto.LogoUrl,
                BannerUrl = dto.BannerUrl,
                IsApproved = dto.IsApproved,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Vendors.Add(vendor);
            await _context.SaveChangesAsync();

            await _codeAssignment.AssignVendorCodeAsync(vendor);

            if (dto.IsApproved && !await _userManager.IsInRoleAsync(user, "Vendor"))
                await _userManager.AddToRoleAsync(user, "Vendor");

            vendor = await _context.Vendors
                .Include(v => v.User)
                .Include(v => v.Products)
                .FirstAsync(v => v.Id == vendor.Id);

            return StatusCode(201, new ApiResult(
                data: MapToDto(vendor),
                message: "Vendor created successfully"));
        }


        [HttpPut("{vendorCode}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdateVendor(
            string vendorCode, [FromBody] AdminUpdateVendorDto dto)
        {
            var id = await _codeLookup.ResolveVendorIdAsync(vendorCode);

            var vendor = await _context.Vendors
                .Include(v => v.User)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (vendor == null)
                throw new NotFoundException("Vendor not found");

            if (!string.IsNullOrWhiteSpace(dto.StoreName) &&
                !dto.StoreName.Equals(vendor.StoreName, StringComparison.OrdinalIgnoreCase))
            {
                var nameExists = await _context.Vendors
                    .AnyAsync(v => v.Id != id &&
                                   v.StoreName.ToLower() == dto.StoreName.ToLower());
                if (nameExists)
                    throw new ConflictException("A vendor with this store name already exists");

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
            if (dto.Governorate.HasValue)
                vendor.Governorate = dto.Governorate.Value;
            if (dto.LogoUrl != null) vendor.LogoUrl = dto.LogoUrl;
            if (dto.BannerUrl != null) vendor.BannerUrl = dto.BannerUrl;

            if (dto.IsApproved.HasValue && dto.IsApproved.Value != vendor.IsApproved)
            {
                vendor.IsApproved = dto.IsApproved.Value;

                var appUser = await _userManager.FindByIdAsync(vendor.UserId);
                if (appUser != null)
                {
                    if (dto.IsApproved.Value && !await _userManager.IsInRoleAsync(appUser, "Vendor"))
                        await _userManager.AddToRoleAsync(appUser, "Vendor");
                    else if (!dto.IsApproved.Value && await _userManager.IsInRoleAsync(appUser, "Vendor"))
                        await _userManager.RemoveFromRoleAsync(appUser, "Vendor");
                }
            }

            if (dto.IsActive.HasValue)
                vendor.IsActive = dto.IsActive.Value;

            vendor.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Reload full nav
            vendor = await _context.Vendors
                .Include(v => v.User)
                .Include(v => v.Products)
                .FirstAsync(v => v.Id == id);

            return Ok(new ApiResult(
                data: MapToDto(vendor),
                message: "Vendor updated successfully"));
        }

        
        [HttpDelete("{vendorCode}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteVendor(string vendorCode)
        {
            var id = await _codeLookup.ResolveVendorIdAsync(vendorCode);

            var vendor = await _context.Vendors
                .Include(v => v.User)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (vendor == null)
                throw new NotFoundException("Vendor not found");

            var appUser = await _userManager.FindByIdAsync(vendor.UserId);
            if (appUser != null && await _userManager.IsInRoleAsync(appUser, "Vendor"))
                await _userManager.RemoveFromRoleAsync(appUser, "Vendor");

            _context.Vendors.Remove(vendor);
            await _context.SaveChangesAsync();

            return Ok(new ApiResult(message: "Vendor deleted successfully"));
        }

        
        [HttpPost("{vendorCode}/approve")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ApproveVendor(
            string vendorCode, [FromBody] VendorApprovalDto? dto = null)
        {
            var id = await _codeLookup.ResolveVendorIdAsync(vendorCode);
            var result = await _vendorService.ApproveVendorAsync(id, isApproved: true);
            return Ok(new ApiResult(data: result, message: "Vendor approved successfully"));
        }

        
        [HttpPost("{vendorCode}/reject")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RejectVendor(
            string vendorCode, [FromBody] VendorApprovalDto dto)
        {
            if (!dto.IsApproved && string.IsNullOrWhiteSpace(dto.RejectionReason))
                throw new BadRequestException("A rejection reason is required when rejecting a vendor");

            var id = await _codeLookup.ResolveVendorIdAsync(vendorCode);
            var result = await _vendorService.ApproveVendorAsync(
                id, isApproved: false, rejectionReason: dto.RejectionReason);

            return Ok(new ApiResult(data: result, message: "Vendor rejected"));
        }

        
        [HttpPost("{vendorCode}/toggle-status")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ToggleVendorStatus(string vendorCode)
        {
            var id = await _codeLookup.ResolveVendorIdAsync(vendorCode);
            var result = await _vendorService.ToggleVendorStatusAsync(id);
            var msg = result.IsActive ? "Vendor activated" : "Vendor deactivated";
            return Ok(new ApiResult(data: result, message: msg));
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
            governorate = v.Governorate.ToString(),
            isApproved = v.IsApproved,
            isActive = v.IsActive,
            totalProducts = v.Products?.Count ?? 0,
            createdAt = v.CreatedAt,
            updatedAt = v.UpdatedAt
        };
    }
}
