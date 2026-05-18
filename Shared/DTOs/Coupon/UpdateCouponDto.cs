using Graduation.DAL.Entities;

namespace Shared.DTOs.Coupon
{
    public class UpdateCouponDto
    {
        public DiscountType? DiscountType { get; set; }
        public decimal? DiscountValue { get; set; }
        public decimal? MinOrderAmount { get; set; }
        public int? MaxUsageCount { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public int? VendorId { get; set; }
    }
}
