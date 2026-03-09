using Shared.DTOs.Review;

namespace Graduation.BLL.Services.Interfaces
{
    public interface IReviewService
    {
        Task<ReviewDto> CreateReviewAsync(string userId, CreateReviewDto dto);
        Task<List<ReviewDto>> GetProductReviewsAsync(int productId, bool approvedOnly = true);
        Task<List<ReviewDto>> GetPendingReviewsAsync();
        Task<List<ReviewDto>> GetPendingReviewsAsync(int vendorId);
        Task<List<ReviewDto>> GetUserReviewsAsync(string userId);
        Task<bool> DeleteReviewAsync(int id, string userId, bool isAdmin = false);
        Task<bool> ApproveReviewAsync(int id, bool isApproved = true);
        Task<ReviewDto?> GetReviewByIdAsync(int reviewId);
    }
}
