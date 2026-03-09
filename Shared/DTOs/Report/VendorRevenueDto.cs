namespace Shared.DTOs.Report
{
    public class VendorRevenueDto
    {
        public int VendorId { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public decimal TotalRevenue { get; set; }
        public int TotalOrders { get; set; }
    }
}