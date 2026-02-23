using Shared.DTOs.Review;

namespace Graduation.BLL.Services.Interfaces
{
    public interface IReviewService
    {
        Task<ReviewDto> CreateReviewAsync(string userId, CreateReviewDto dto);
        Task<List<ReviewDto>> GetProductReviewsAsync(int productId);
        Task<List<ReviewDto>> GetPendingReviewsAsync();
        Task<List<ReviewDto>> GetUserReviewsAsync(string userId);
        Task<bool> DeleteReviewAsync(int reviewId, string userId);
        Task<bool> ApproveReviewAsync(int reviewId);
        Task<ReviewDto?> GetReviewByIdAsync(int reviewId);
    }
}
