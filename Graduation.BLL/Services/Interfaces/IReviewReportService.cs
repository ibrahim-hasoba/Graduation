using Shared.DTOs;
using Shared.DTOs.Review;

namespace Graduation.BLL.Services.Interfaces
{
    public interface IReviewReportService
    {
        Task<ReviewReportDto> CreateReportAsync(string userId, CreateReviewReportDto dto);
        Task<PagedResult<ReviewReportDto>> GetPendingReportsAsync(int pageNumber, int pageSize);
        Task<ReviewReportDto?> GetReportByIdAsync(int reportId);
        Task<bool> ApproveReportAsync(int reportId, string adminUserId);
        Task<bool> DismissReportAsync(int reportId, string adminUserId);
        Task<bool> DeleteReviewFromReportAsync(int reportId, string adminUserId);
    }
}
