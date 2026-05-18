using Graduation.DAL.Entities;
using System.ComponentModel.DataAnnotations;

namespace Shared.DTOs.Coupon
{
    public class CreateCouponDto
    {
        [Required] public string Code { get; set; } = string.Empty;
        [Required] public DiscountType DiscountType { get; set; }
        [Required] [Range(0.01, double.MaxValue)] public decimal DiscountValue { get; set; }
        [Range(0, double.MaxValue)] public decimal? MinOrderAmount { get; set; }
        [Range(1, int.MaxValue)] public int? MaxUsageCount { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public int? VendorId { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
