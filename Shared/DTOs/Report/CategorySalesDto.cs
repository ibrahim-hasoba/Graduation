namespace Shared.DTOs.Report
{
    public class CategorySalesDto
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public decimal TotalSales { get; set; }
        public int TotalOrders { get; set; }
        public int TotalItemsSold { get; set; }
    }
}