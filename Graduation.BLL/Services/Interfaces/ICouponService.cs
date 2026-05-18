using Shared.DTOs.Coupon;

namespace Graduation.BLL.Services.Interfaces
{
    public interface ICouponService
    {
        Task<CouponDto> CreateAsync(CreateCouponDto dto);
        Task<CouponDto> UpdateAsync(int id, UpdateCouponDto dto);
        Task DeleteAsync(int id);
        Task<CouponDto?> GetByIdAsync(int id);
        Task<List<CouponDto>> GetAllAsync();
        Task<ApplyCouponResultDto> ValidateAndCalculateAsync(string code, decimal orderSubTotal);
    }
}
