using Shared.DTOs.Report;

namespace Graduation.BLL.Services.Interfaces
{
    public interface IReportService
    {
        Task<SalesReportDto> GetSalesReportAsync(DateTime startDate, DateTime endDate, int? vendorId = null);
        Task<List<CategorySalesDto>> GetSalesByCategoryAsync(DateTime startDate, DateTime endDate);
        Task<VendorPerformanceDto> GetVendorPerformanceAsync(int vendorId);
        Task<CustomerInsightsDto> GetCustomerInsightsAsync();
        Task<List<LowStockProductDto>> GetLowStockProductsAsync(int threshold = 10);
        Task<List<VendorRevenueDto>> GetRevenueByVendorAsync(DateTime startDate, DateTime endDate, int take = 10);
        Task<List<TopProductDto>> GetTopProductsAsync(DateTime startDate, DateTime endDate, int take = 10);
        Task<List<OrderStatusSummaryDto>> GetOrderStatusSummaryAsync();
        Task<List<UserTrendDto>> GetUserTrendsAsync();
    }
}
