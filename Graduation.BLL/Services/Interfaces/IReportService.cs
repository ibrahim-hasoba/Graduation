namespace Graduation.BLL.Services.Interfaces
{
  public interface IReportService
  {
    Task<dynamic> GetSalesReportAsync(DateTime startDate, DateTime endDate, int? vendorId = null);
    Task<dynamic> GetSalesByCategoryAsync(DateTime startDate, DateTime endDate);
    Task<dynamic> GetVendorPerformanceAsync(int vendorId);
    Task<dynamic> GetCustomerInsightsAsync();
    Task<dynamic> GetLowStockProductsAsync(int threshold = 10);
    Task<dynamic> GetRevenueByVendorAsync(DateTime startDate, DateTime endDate, int take = 10);
    Task<dynamic> GetTopProductsAsync(DateTime startDate, DateTime endDate, int take = 10);
    Task<dynamic> GetOrderStatusSummaryAsync();
    Task<dynamic> GetUserTrendsAsync();
  }
}
