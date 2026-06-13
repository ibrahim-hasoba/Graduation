using Graduation.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Graduation.API.Controllers
{
    [Route("api/admin/coupons")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminCouponsController : BaseController
    {
        private readonly ICouponService _couponService;
        private readonly IActivityLogService _activityLog;

        public AdminCouponsController(
            ICouponService couponService,
            IActivityLogService activityLog,
            ILanguageService lang)
            : base(lang)
        {
            _couponService = couponService;
            _activityLog = activityLog;
        }

        /// <summary>Creates a new discount coupon.</summary>
        [HttpPost]
        public async Task<IActionResult> CreateCoupon([FromBody] Shared.DTOs.Coupon.CreateCouponDto dto)
        {
            var coupon = await _couponService.CreateAsync(dto);
            await _activityLog.LogAsync(GetRequiredUserId(), "Create", "Coupon", coupon.Code, $"Created coupon {coupon.Code}");
            return CreatedResult(data: coupon);
        }

        /// <summary>Updates an existing coupon.</summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCoupon(int id, [FromBody] Shared.DTOs.Coupon.UpdateCouponDto dto)
        {
            var coupon = await _couponService.UpdateAsync(id, dto);
            await _activityLog.LogAsync(GetRequiredUserId(), "Update", "Coupon", coupon.Code, $"Updated coupon {coupon.Code}");
            return OkResult(data: coupon);
        }

        /// <summary>Deletes a coupon.</summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCoupon(int id)
        {
            await _couponService.DeleteAsync(id);
            await _activityLog.LogAsync(GetRequiredUserId(), "Delete", "Coupon", id.ToString(), $"Deleted coupon #{id}");
            return OkResult(message: "Coupon deleted");
        }

        /// <summary>Gets all coupons.</summary>
        [HttpGet]
        public async Task<IActionResult> GetAllCoupons()
        {
            var coupons = await _couponService.GetAllAsync();
            return OkResult(data: coupons);
        }

        /// <summary>Gets a single coupon by ID.</summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetCoupon(int id)
        {
            var coupon = await _couponService.GetByIdAsync(id);
            if (coupon == null)
                throw new Shared.Errors.NotFoundException("Coupon", id);
            return OkResult(data: coupon);
        }
    }
}
