namespace Shared.DTOs.Report
{
    public class VendorPerformanceDto
    {
        public int VendorId { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public decimal TotalRevenue { get; set; }
        public int TotalOrders { get; set; }
        public decimal AverageOrderValue { get; set; }
        public int TotalProducts { get; set; }
        public int ActiveProducts { get; set; }
        public List<TopProductDto> TopProducts { get; set; } = new();
        public decimal RevenueLastThirtyDays { get; set; }
    }
}