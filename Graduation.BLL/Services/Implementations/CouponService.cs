using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Graduation.BLL.DTOs.Coupon;
using Graduation.BLL.Errors;

namespace Graduation.BLL.Services.Implementations
{
    public class CouponService : ICouponService
    {
        private readonly DatabaseContext _context;

        public CouponService(DatabaseContext context)
        {
            _context = context;
        }

        public async Task<CouponDto> CreateAsync(CreateCouponDto dto)
        {
            if (await _context.Coupons.AnyAsync(c => c.Code == dto.Code.ToUpper()))
                throw new BadRequestException($"Coupon code '{dto.Code.ToUpper()}' already exists");

            var coupon = new Coupon
            {
                Code = dto.Code.ToUpper(),
                DiscountType = dto.DiscountType,
                DiscountValue = dto.DiscountValue,
                MinOrderAmount = dto.MinOrderAmount,
                MaxUsageCount = dto.MaxUsageCount,
                ExpiresAt = dto.ExpiresAt,
                VendorId = dto.VendorId,
                IsActive = dto.IsActive,
                CreatedAt = DateTime.UtcNow,
            };

            _context.Coupons.Add(coupon);
            await _context.SaveChangesAsync();
            return await GetByIdInternalAsync(coupon.Id);
        }

        public async Task<CouponDto> UpdateAsync(int id, UpdateCouponDto dto)
        {
            var coupon = await _context.Coupons.FindAsync(id)
                ?? throw new NotFoundException("Coupon", id);
            if (dto.DiscountType.HasValue) coupon.DiscountType = dto.DiscountType.Value;
            if (dto.DiscountValue.HasValue) coupon.DiscountValue = dto.DiscountValue.Value;
            if (dto.MinOrderAmount.HasValue) coupon.MinOrderAmount = dto.MinOrderAmount;
            if (dto.MaxUsageCount.HasValue) coupon.MaxUsageCount = dto.MaxUsageCount;
            if (dto.IsActive.HasValue) coupon.IsActive = dto.IsActive.Value;
            if (dto.ExpiresAt.HasValue) coupon.ExpiresAt = dto.ExpiresAt;
            if (dto.VendorId.HasValue) coupon.VendorId = dto.VendorId;
            await _context.SaveChangesAsync();
            return await GetByIdInternalAsync(id);
        }

        public async Task DeleteAsync(int id)
        {
            var coupon = await _context.Coupons.FindAsync(id)
                ?? throw new NotFoundException("Coupon", id);
            _context.Coupons.Remove(coupon);
            await _context.SaveChangesAsync();
        }

        public async Task<CouponDto?> GetByIdAsync(int id)
        {
            var coupon = await _context.Coupons.FindAsync(id);
            return coupon == null ? null : MapToDto(coupon);
        }

        public async Task<List<CouponDto>> GetAllAsync()
        {
            return await _context.Coupons
                .Include(c => c.Vendor)
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => MapToDto(c))
                .ToListAsync();
        }

        public async Task<ApplyCouponResultDto> ValidateAndCalculateAsync(string code, decimal orderSubTotal)
        {
            var coupon = await _context.Coupons.FirstOrDefaultAsync(c => c.Code == code.ToUpper())
                ?? throw new NotFoundException("Coupon not found");

            if (!coupon.IsActive)
                throw new BadRequestException("Coupon is no longer active");
            if (coupon.ExpiresAt.HasValue && coupon.ExpiresAt < DateTime.UtcNow)
                throw new BadRequestException("Coupon has expired");
            if (coupon.MaxUsageCount.HasValue && coupon.CurrentUsageCount >= coupon.MaxUsageCount.Value)
                throw new BadRequestException("Coupon usage limit has been reached");
            if (coupon.MinOrderAmount.HasValue && orderSubTotal < coupon.MinOrderAmount.Value)
                throw new BadRequestException($"Minimum order amount of {coupon.MinOrderAmount.Value} required for this coupon");

            var discountAmount = coupon.DiscountType == DiscountType.Percentage
                ? Math.Round(orderSubTotal * coupon.DiscountValue / 100m, 2)
                : coupon.DiscountValue;

            if (discountAmount > orderSubTotal)
                discountAmount = orderSubTotal;

            return new ApplyCouponResultDto
            {
                Code = coupon.Code,
                DiscountType = coupon.DiscountType.ToString(),
                DiscountValue = coupon.DiscountValue,
                DiscountAmount = discountAmount,
                SubTotalAfterDiscount = orderSubTotal - discountAmount,
            };
        }

        private async Task<CouponDto> GetByIdInternalAsync(int id)
        {
            var coupon = await _context.Coupons.Include(c => c.Vendor).FirstAsync(c => c.Id == id);
            return MapToDto(coupon);
        }

        private static CouponDto MapToDto(Coupon c) => new()
        {
            Id = c.Id,
            Code = c.Code,
            DiscountType = c.DiscountType.ToString(),
            DiscountValue = c.DiscountValue,
            MinOrderAmount = c.MinOrderAmount,
            MaxUsageCount = c.MaxUsageCount,
            CurrentUsageCount = c.CurrentUsageCount,
            IsActive = c.IsActive,
            CreatedAt = c.CreatedAt,
            ExpiresAt = c.ExpiresAt,
            VendorId = c.VendorId,
            VendorName = c.Vendor?.StoreName,
        };
    }
}
