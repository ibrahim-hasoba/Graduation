using Graduation.BLL.DTOs.Admin;
using System;
using System.Collections.Generic;
using System.Text;

namespace Graduation.BLL.Services.Interfaces
{
    public interface IAdminService
    {
        Task<DashboardStatsDto> GetDashboardStatsAsync();
        Task<List<TopProductDto>> GetTopProductsAsync(int count = 10);
        Task<List<TopVendorDto>> GetTopVendorsAsync(int count = 10);
        Task<SalesChartDto> GetSalesChartDataAsync();
        Task<UserStatsDto> GetUserStatsAsync();
    }
}
