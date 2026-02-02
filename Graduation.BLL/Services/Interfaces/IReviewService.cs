using Shared.DTOs.Review;
using System;
using System.Collections.Generic;
using System.Text;

namespace Graduation.BLL.Services.Interfaces
{
    public interface IReviewService
    {
        Task<ReviewDto> CreateReviewAsync(string userId, CreateReviewDto dto);
        Task<List<ReviewDto>> GetProductReviewsAsync(int productId, bool approvedOnly = true);
        Task<List<ReviewDto>> GetUserReviewsAsync(string userId);
        Task<bool> DeleteReviewAsync(int reviewId, string userId);
        Task<bool> ApproveReviewAsync(int reviewId); // Admin only
        Task<ReviewDto?> GetReviewByIdAsync(int reviewId);
    }
}
