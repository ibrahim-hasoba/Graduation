using Shared.DTOs;
using Shared.DTOs.Review;

namespace Graduation.BLL.Services.Interfaces
{
    public interface IReviewService
    {
        Task<ReviewDto> CreateReviewAsync(string userId, CreateReviewDto dto);
        Task<PagedResult<ReviewDto>> GetProductReviewsAsync(int productId, int pageNumber, int pageSize, bool approvedOnly = true);
        Task<PagedResult<ReviewDto>> GetPendingReviewsAsync(int pageNumber, int pageSize);
        Task<List<ReviewDto>> GetPendingReviewsAsync(int vendorId);
        Task<PagedResult<ReviewDto>> GetUserReviewsAsync(string userId, int pageNumber, int pageSize);
        Task<bool> DeleteReviewAsync(int id, string userId, bool isAdmin = false);
        Task<bool> ApproveReviewAsync(int id, bool isApproved = true);
        Task<ReviewDto?> GetReviewByIdAsync(int reviewId);
    }
}
