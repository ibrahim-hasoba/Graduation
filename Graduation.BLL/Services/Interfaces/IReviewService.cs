using Shared.DTOs.Review;

namespace Graduation.BLL.Services.Interfaces
{
    public interface IReviewService
    {
        Task<ReviewDto> CreateReviewAsync(string userId, CreateReviewDto dto);

        /// <summary>
        /// Get reviews for a product.
        /// FIXED: Pass productId = 0 to retrieve reviews across ALL products (used by admin pending endpoint).
        /// </summary>
        Task<List<ReviewDto>> GetProductReviewsAsync(int productId, bool approvedOnly = true);

        Task<List<ReviewDto>> GetUserReviewsAsync(string userId);
        Task<bool> DeleteReviewAsync(int reviewId, string userId);
        Task<bool> ApproveReviewAsync(int reviewId);
        Task<ReviewDto?> GetReviewByIdAsync(int reviewId);
    }
}
