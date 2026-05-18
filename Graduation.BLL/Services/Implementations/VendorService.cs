using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.BackgroundTasks;
using Shared.DTOs;
using Shared.DTOs.Vendor;
using Shared.Errors;

namespace Graduation.BLL.Services.Implementations
{
    public class VendorService : IVendorService
    {
        private readonly DatabaseContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IBackgroundTaskQueue? _taskQueue;
        private readonly ICodeAssignmentService _codeAssignment;
        private readonly ILogger<VendorService> _logger;
        private readonly IContentModerationService _contentModeration;

        public VendorService(
            DatabaseContext context,
            UserManager<AppUser> userManager,
            IServiceScopeFactory scopeFactory,
            ILogger<VendorService> logger,
            ICodeAssignmentService codeAssignment,
            IContentModerationService contentModeration,
            IBackgroundTaskQueue? taskQueue = null)
        {
            _context = context;
            _userManager = userManager;
            _scopeFactory = scopeFactory;
            _taskQueue = taskQueue;
            _codeAssignment = codeAssignment;
            _logger = logger;
            _contentModeration = contentModeration;
        }

        public async Task<VendorDto> RegisterVendorAsync(string userId, VendorRegisterDto dto)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                throw new NotFoundException("User not found");

            if (!user.EmailConfirmed)
                throw new UnauthorizedException(
                    "Email must be verified before registering as a vendor. " +
                    "Please check your inbox for the verification link.");

            var existingVendor = await _context.Vendors
                .FirstOrDefaultAsync(v => v.UserId == userId);
            if (existingVendor != null)
                throw new ConflictException("You already have a vendor account");

            var storeNameExists = await _context.Vendors
                .AnyAsync(v => v.StoreName.ToLower() == dto.StoreName.ToLower());
            if (storeNameExists)
                throw new ConflictException("Store name already exists. Please choose a different name");

            // Run content moderation on vendor information
            var moderationFlags = new List<string>();
            moderationFlags.AddRange(_contentModeration.Moderate(dto.StoreName).Flags);
            moderationFlags.AddRange(_contentModeration.Moderate(dto.StoreNameAr).Flags);
            moderationFlags.AddRange(_contentModeration.Moderate(dto.StoreDescription).Flags);
            moderationFlags.AddRange(_contentModeration.Moderate(dto.StoreDescriptionAr).Flags);
            moderationFlags.AddRange(_contentModeration.Moderate(dto.Address).Flags);
            moderationFlags.AddRange(_contentModeration.Moderate(dto.City).Flags);

            var isClean = moderationFlags.Count == 0;

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
                ApprovalStatus = isClean ? VendorApprovalStatus.Approved : VendorApprovalStatus.Pending,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude
            };

            _context.Vendors.Add(vendor);
            await _context.SaveChangesAsync();

            if (isClean)
            {
                // Auto-approved: add Vendor role
                if (!await _userManager.IsInRoleAsync(user, "Vendor"))
                    await _userManager.AddToRoleAsync(user, "Vendor");

                _logger.LogInformation(
                    "Vendor auto-approved: {StoreName} by user {UserId} (clean content)",
                    dto.StoreName, userId);
            }
            else
            {
                _logger.LogInformation(
                    "Vendor flagged for review: {StoreName} by user {UserId}. Flags: {Flags}",
                    dto.StoreName, userId, string.Join("; ", moderationFlags));
            }

            await _codeAssignment.AssignVendorCodeAsync(vendor);

            return await GetVendorByIdAsync(vendor.Id);
        }

        public async Task<VendorDto> GetVendorByIdAsync(int id)
        {
            var vendor = await _context.Vendors
                .Include(v => v.User)
                .Include(v => v.Products)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (vendor == null)
                throw new NotFoundException("Vendor", id);

            return MapToDto(vendor);
        }

        public async Task<VendorDto?> GetVendorByUserIdAsync(string userId)
        {
            var vendor = await _context.Vendors
                .Include(v => v.User)
                .Include(v => v.Products)
                .FirstOrDefaultAsync(v => v.UserId == userId);

            return vendor == null ? null : MapToDto(vendor);
        }

        public async Task<IEnumerable<VendorListDto>> GetAllVendorsAsync(bool? isApproved = null)
        {
            var query = _context.Vendors
                .Include(v => v.Products)
                .AsQueryable();

            if (isApproved.HasValue)
            {
                var targetStatus = isApproved.Value
                    ? VendorApprovalStatus.Approved
                    : VendorApprovalStatus.Pending;
                query = query.Where(v => v.ApprovalStatus == targetStatus);
            }

            var vendors = await query
                .OrderByDescending(v => v.CreatedAt)
                .ToListAsync();

            return vendors.Select(v => new VendorListDto
            {
                Id = v.Id,
                StoreName = v.StoreName,
                StoreNameAr = v.StoreNameAr,
                LogoUrl = v.LogoUrl,
                City = v.City,
                IsApproved = v.IsApproved,
                ApprovalStatus = v.ApprovalStatus.ToString(),
                IsActive = v.IsActive,
                TotalProducts = v.Products.Count,
                CreatedAt = v.CreatedAt
            });
        }

        public async Task<VendorDto> UpdateVendorAsync(int id, string userId, VendorUpdateDto dto)
        {
            var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.Id == id);
            if (vendor == null)
                throw new NotFoundException("Vendor", id);

            if (vendor.UserId != userId)
                throw new UnauthorizedException("You are not authorized to update this vendor");

            var storeNameExists = await _context.Vendors
                .AnyAsync(v => v.Id != id && v.StoreName.ToLower() == dto.StoreName.ToLower());
            if (storeNameExists)
                throw new ConflictException("Store name already exists. Please choose a different name");

            vendor.StoreName = dto.StoreName;
            vendor.StoreNameAr = dto.StoreNameAr;
            vendor.StoreDescription = dto.StoreDescription;
            vendor.StoreDescriptionAr = dto.StoreDescriptionAr;
            vendor.PhoneNumber = dto.PhoneNumber;
            vendor.Address = dto.Address;
            vendor.City = dto.City;
            vendor.LogoUrl = dto.LogoUrl;
            vendor.BannerUrl = dto.BannerUrl;
            vendor.UpdatedAt = DateTime.UtcNow;
            vendor.Latitude = dto.Latitude;
            vendor.Longitude = dto.Longitude;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Vendor updated: {VendorId} - {StoreName}", id, dto.StoreName);

            return await GetVendorByIdAsync(id);
        }

        public async Task<VendorDto> ApproveVendorAsync(
            int id,
            bool isApproved,
            string? rejectionReason = null)
        {
            var vendor = await _context.Vendors
                .Include(v => v.User)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (vendor == null)
                throw new NotFoundException("Vendor", id);

            if (isApproved)
            {
                vendor.ApprovalStatus = VendorApprovalStatus.Approved;
                vendor.RejectionReason = null;
            }
            else
            {
                vendor.ApprovalStatus = VendorApprovalStatus.Rejected;
                vendor.RejectionReason = rejectionReason;
            }

            vendor.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            if (vendor.User != null)
            {
                var appUser = await _userManager.FindByIdAsync(vendor.UserId);
                if (appUser != null)
                {
                    var isInVendorRole = await _userManager.IsInRoleAsync(appUser, "Vendor");

                    if (isApproved && !isInVendorRole)
                    {
                        await _userManager.AddToRoleAsync(appUser, "Vendor");
                        _logger.LogInformation(
                            "User {UserId} added to Vendor role after approval", vendor.UserId);
                    }
                    else if (!isApproved && isInVendorRole)
                    {
                        await _userManager.RemoveFromRoleAsync(appUser, "Vendor");
                        _logger.LogInformation(
                            "User {UserId} removed from Vendor role after rejection", vendor.UserId);
                    }
                }
            }

            if (vendor.User != null && !string.IsNullOrEmpty(vendor.User.Email))
            {
                var emailCopy = vendor.User.Email;
                var storeNameCopy = vendor.StoreName;

                if (_taskQueue != null)
                {
                    _taskQueue.QueueBackgroundWorkItem(async (sp, token) =>
                    {
                        var emailService = sp.GetRequiredService<IEmailService>();
                        await emailService.SendVendorApprovalEmailAsync(
                            emailCopy, storeNameCopy, isApproved, rejectionReason);
                    });
                }
                else
                {
                    var scope = _scopeFactory.CreateScope();
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                            await emailService.SendVendorApprovalEmailAsync(
                                emailCopy, storeNameCopy, isApproved, rejectionReason);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex,
                                "Failed to send vendor approval email to {Email}", emailCopy);
                        }
                        finally
                        {
                            scope.Dispose();
                        }
                    });
                }
            }

            _logger.LogInformation(
                "Vendor {VendorId} approval status changed to {Status}",
                id, vendor.ApprovalStatus);

            return await GetVendorByIdAsync(id);
        }

        public async Task<VendorDto> ToggleVendorStatusAsync(int id)
        {
            var vendor = await _context.Vendors.FindAsync(id);
            if (vendor == null)
                throw new NotFoundException("Vendor", id);

            vendor.IsActive = !vendor.IsActive;
            vendor.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Vendor {VendorId} active status toggled to {IsActive}", id, vendor.IsActive);

            return await GetVendorByIdAsync(id);
        }

        public async Task DeleteVendorAsync(int id)
        {
            var vendor = await _context.Vendors.FindAsync(id);
            if (vendor == null)
                throw new NotFoundException("Vendor", id);

            _context.Vendors.Remove(vendor);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Vendor deleted: {VendorId} - {StoreName}", id, vendor.StoreName);
        }

        public async Task<PagedResult<PublicVendorDto>> GetPublicVendorsListAsync(
            int pageNumber = 1,
            int pageSize = 10)
        {
            var query = _context.Vendors
                .Where(v => v.IsActive && v.ApprovalStatus == VendorApprovalStatus.Approved);

            var totalCount = await query.CountAsync();

            var vendors = await query
                .OrderByDescending(v => v.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var vendorIds = vendors.Select(v => v.Id).ToList();

            var ratingStats = await _context.ProductReviews
                .Where(r => r.IsApproved && vendorIds.Contains(r.Product.VendorId))
                .GroupBy(r => r.Product.VendorId)
                .Select(g => new
                {
                    VendorId = g.Key,
                    AverageRating = g.Average(r => (double)r.Rating),
                    TotalReviews = g.Count()
                })
                .ToListAsync();

            var ratingDict = ratingStats.ToDictionary(x => x.VendorId);

            var result = vendors.Select(v =>
            {
                var stats = ratingDict.GetValueOrDefault(v.Id);
                return new PublicVendorDto
                {
                    Id = v.Id,
                    StoreName = v.StoreName,
                    StoreNameAr = v.StoreNameAr ?? string.Empty,
                    LogoUrl = v.LogoUrl ?? string.Empty,
                    AverageRating = stats != null ? Math.Round(stats.AverageRating, 1) : 0.0,
                    TotalReviews = stats?.TotalReviews ?? 0
                };
            }).ToList();

            return new PagedResult<PublicVendorDto>
            {
                Items = result,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<PublicVendorDetailsDto> GetPublicVendorDetailsAsync(int id)
        {
            var vendor = await _context.Vendors
                .Where(v => v.Id == id && v.IsActive && v.ApprovalStatus == VendorApprovalStatus.Approved)
                .Select(v => new PublicVendorDetailsDto
                {
                    Id = v.Id,
                    StoreName = v.StoreName,
                    StoreNameAr = v.StoreNameAr ?? string.Empty,
                    Description = v.StoreDescription ?? string.Empty,
                    LogoUrl = v.LogoUrl ?? string.Empty,
                    BannerImageUrl = v.BannerUrl ?? string.Empty,
                    JoinedDate = v.CreatedAt,
                    AverageRating = 0,
                    TotalReviews = 0
                })
                .FirstOrDefaultAsync();

            if (vendor == null)
                throw new NotFoundException("Vendor not found, or the store is currently inactive.");

            var ratingStats = await _context.ProductReviews
                .Where(r => r.IsApproved && r.Product.VendorId == id)
                .GroupBy(r => r.Product.VendorId)
                .Select(g => new
                {
                    AverageRating = g.Average(r => (double)r.Rating),
                    TotalReviews = g.Count()
                })
                .FirstOrDefaultAsync();

            if (ratingStats != null)
            {
                vendor.AverageRating = Math.Round(ratingStats.AverageRating, 1);
                vendor.TotalReviews = ratingStats.TotalReviews;
            }

            return vendor;
        }
        public async Task UpdateOrderLocationAsync(int orderId, double lat, double lng)
        {
            var order = await _context.Orders.FindAsync(orderId);

            if (order == null)
                throw new NotFoundException("Order", orderId);

            order.CurrentLatitude = lat;
            order.CurrentLongitude = lng;

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Location updated for Order {OrderId}: Lat {Lat}, Lng {Lng}", orderId, lat, lng);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating location for Order {OrderId}", orderId);
                throw;
            }
        }
        private static VendorDto MapToDto(Vendor vendor) => new()
        {
            Id = vendor.Id,
            Code = vendor.Code,
            UserId = vendor.UserId,
            UserEmail = vendor.User?.Email ?? string.Empty,
            UserFullName = $"{vendor.User?.FirstName} {vendor.User?.LastName}",
            StoreName = vendor.StoreName,
            StoreNameAr = vendor.StoreNameAr,
            StoreDescription = vendor.StoreDescription,
            StoreDescriptionAr = vendor.StoreDescriptionAr,
            LogoUrl = vendor.LogoUrl,
            BannerUrl = vendor.BannerUrl,
            PhoneNumber = vendor.PhoneNumber,
            Address = vendor.Address,
            City = vendor.City,
            Latitude = vendor.Latitude,
            Longitude = vendor.Longitude,
            ApprovalStatus = vendor.ApprovalStatus.ToString(),
            ApprovalStatusId = (int)vendor.ApprovalStatus,
            RejectionReason = vendor.RejectionReason,
            IsApproved = vendor.IsApproved,
            IsActive = vendor.IsActive,
            TotalProducts = vendor.Products?.Count ?? 0,
            TotalOrders = 0,
            CreatedAt = vendor.CreatedAt,
            UpdatedAt = vendor.UpdatedAt,
        };
    }
}