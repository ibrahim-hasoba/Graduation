namespace Graduation.BLL.DTOs.Coupon
{
    public class ApplyCouponResultDto
    {
        public string Code { get; set; } = string.Empty;
        public string DiscountType { get; set; } = string.Empty;
        public decimal DiscountValue { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal SubTotalAfterDiscount { get; set; }
    }
}
