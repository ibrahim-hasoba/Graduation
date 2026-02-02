using Graduation.API.Errors;
using Graduation.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shared.DTOs.Review;

namespace Graduation.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReviewsController : ControllerBase
    {
        private readonly IReviewService _reviewService;

        public ReviewsController(IReviewService reviewService)
        {
            _reviewService = reviewService;
        }

        /// <summary>
        /// Get product reviews (public)
        /// </summary>
        [HttpGet("product/{productId}")]
        public async Task<IActionResult> GetProductReviews(int productId)
        {
            var reviews = await _reviewService.GetProductReviewsAsync(productId, approvedOnly: true);
            return Ok(new { success = true, data = reviews });
        }

        /// <summary>
        /// Get my reviews (authenticated)
        /// </summary>
        [HttpGet("my-reviews")]
        [Authorize]
        public async Task<IActionResult> GetMyReviews()
        {
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            var reviews = await _reviewService.GetUserReviewsAsync(userId);
            return Ok(new { success = true, data = reviews });
        }

        /// <summary>
        /// Create review (authenticated, must have purchased)
        /// </summary>
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateReview([FromBody] CreateReviewDto dto)
        {
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            var review = await _reviewService.CreateReviewAsync(userId, dto);

            return StatusCode(201, new
            {
                success = true,
                message = "Review submitted successfully. It will be visible after admin approval.",
                data = review
            });
        }

        /// <summary>
        /// Delete my review (authenticated)
        /// </summary>
        [HttpDelete("{reviewId}")]
        [Authorize]
        public async Task<IActionResult> DeleteReview(int reviewId)
        {
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            var deleted = await _reviewService.DeleteReviewAsync(reviewId, userId);

            if (!deleted)
                throw new NotFoundException("Review not found or you don't have permission to delete it");

            return Ok(new { success = true, message = "Review deleted successfully" });
        }

        /// <summary>
        /// Approve review (admin only)
        /// </summary>
        [HttpPost("{reviewId}/approve")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ApproveReview(int reviewId)
        {
            var approved = await _reviewService.ApproveReviewAsync(reviewId);

            if (!approved)
                throw new NotFoundException("Review not found");

            return Ok(new { success = true, message = "Review approved successfully" });
        }

        /// <summary>
        /// Get pending reviews (admin only)
        /// </summary>
        [HttpGet("pending")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetPendingReviews()
        {
            // This would need to be implemented in the service
            // For now, return empty list or implement later
            return Ok(new { success = true, data = new List<ReviewDto>() });
        }
    }
}
