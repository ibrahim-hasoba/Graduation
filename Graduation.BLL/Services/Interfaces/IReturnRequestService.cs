using Shared.DTOs.ReturnRequest;

namespace Graduation.BLL.Services.Interfaces
{
    public interface IReturnRequestService
    {
        Task<ReturnRequestDto> CreateAsync(string userId, CreateReturnRequestDto dto);
        Task<ReturnRequestDto> UpdateStatusAsync(int returnId, string reviewerId, UpdateReturnStatusDto dto);
        Task<List<ReturnRequestDto>> GetByOrderAsync(int orderId);
        Task<List<ReturnRequestDto>> GetByUserAsync(string userId);
        Task<List<ReturnRequestDto>> GetAllAsync();
    }
}
