using System.ComponentModel.DataAnnotations;

namespace Graduation.BLL.DTOs.Coupon
{
    public class ApplyCouponDto
    {
        [Required] public string Code { get; set; } = string.Empty;
        [Required] [Range(0, double.MaxValue)] public decimal OrderSubTotal { get; set; }
    }
}
