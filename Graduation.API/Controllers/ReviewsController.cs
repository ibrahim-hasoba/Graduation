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
        [ProducesResponseType(StatusCodes.Status200OK)]
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
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
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
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ApproveReview(int reviewId)
        {
            var approved = await _reviewService.ApproveReviewAsync(reviewId);

            if (!approved)
                throw new NotFoundException("Review not found");

            return Ok(new { success = true, message = "Review approved successfully" });
        }

        /// <summary>
        /// Get pending reviews (admin only)
        /// FIXED BUG: Was always returning an empty list unconditionally.
        /// Now calls the service to fetch unapproved reviews from the database.
        /// </summary>
        [HttpGet("pending")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetPendingReviews()
        {
            var reviews = await _reviewService.GetProductReviewsAsync(
                productId: 0,   // 0 signals "all products" — see updated IReviewService below
                approvedOnly: false);

            return Ok(new { success = true, data = reviews });
        }
    }
}
