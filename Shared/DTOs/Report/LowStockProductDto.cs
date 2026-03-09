namespace Shared.DTOs.Report
{
    public class LowStockProductDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int CurrentStock { get; set; }
        public int VendorId { get; set; }
        public string VendorName { get; set; } = string.Empty;
    }
}