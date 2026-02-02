using Shared.DTOs.Order;
using System;
using System.Collections.Generic;
using System.Text;

namespace Graduation.BLL.Services.Interfaces
{
    public interface IOrderService
    {
        Task<OrderDto> CreateOrderAsync(string userId, CreateOrderDto dto);
        Task<OrderDto> GetOrderByIdAsync(int id, string userId);
        Task<List<OrderListDto>> GetUserOrdersAsync(string userId);
        Task<List<OrderListDto>> GetVendorOrdersAsync(int vendorId);
        Task<OrderDto> UpdateOrderStatusAsync(int id, int vendorId, UpdateOrderStatusDto dto);
        Task<OrderDto> CancelOrderAsync(int id, string userId, string reason);
    }
}
