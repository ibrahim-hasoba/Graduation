using Shared.DTOs;
using Shared.DTOs.Order;
using System;
using System.Collections.Generic;
using System.Text;

namespace Graduation.BLL.Services.Interfaces
{
    public interface IOrderService
    {
        Task<CreateOrderResultDto> CreateOrderAsync(string userId, CreateOrderDto dto);
        Task<OrderDto> GetOrderByIdAsync(int id, string userId);
        Task<PagedResult<OrderListDto>> GetUserOrdersAsync(string userId, int pageNumber = 1, int pageSize = 10);
        Task<List<OrderListDto>> GetVendorOrdersAsync(int vendorId);
        Task<OrderDto> UpdateOrderStatusAsync(int id, int vendorId, UpdateOrderStatusDto dto);
        Task<OrderDto> CancelOrderAsync(int id, string userId, string reason);
        Task HandleUserAccountDeletionAsync(string userId);
        Task<OrderMapTrackingDto> GetOrderMapTrackingAsync(string orderNumber, string userId);
    }
}
