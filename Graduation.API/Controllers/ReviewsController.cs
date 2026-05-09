using Shared.Errors;
using Graduation.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Graduation.API.Extensions;
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

        [HttpGet("product/{productId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetProductReviews(
                int productId,
                [FromQuery] int pageNumber = 1,
                [FromQuery] int pageSize = 10)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1 || pageSize > 50) pageSize = 10;

            var result = await _reviewService.GetProductReviewsAsync(productId, pageNumber, pageSize);
            return Ok(new ApiResult(data: result, count: result.TotalCount));
        }

        [HttpGet("my-reviews")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMyReviews(
                 [FromQuery] int pageNumber = 1,
                 [FromQuery] int pageSize = 10)
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1 || pageSize > 50) pageSize = 10;

            var result = await _reviewService.GetUserReviewsAsync(userId, pageNumber, pageSize);
            return Ok(new ApiResult(data: result, count: result.TotalCount));
        }

        [HttpPost]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CreateReview([FromBody] CreateReviewDto dto)
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            var review = await _reviewService.CreateReviewAsync(userId, dto);
            return StatusCode(201, new ApiResult(
                data: review,
                message: "Review submitted successfully. It will be visible after admin approval."));
        }

        [HttpDelete("{reviewId}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteReview(int reviewId)
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            // FIX: DeleteReviewAsync now returns bool instead of throwing
            var deleted = await _reviewService.DeleteReviewAsync(reviewId, userId);
            if (!deleted)
                throw new NotFoundException("Review not found or you don't have permission to delete it");

            return Ok(new ApiResult(message: "Review deleted successfully"));
        }
        
    }
}
