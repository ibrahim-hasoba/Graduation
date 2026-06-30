using Graduation.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Graduation.API.Extensions;
using Graduation.BLL.DTOs.Review;

namespace Graduation.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReviewsController : BaseController
    {
        private readonly IReviewService _reviewService;
        private readonly IReviewReportService _reviewReportService;

        public ReviewsController(
            IReviewService reviewService,
            IReviewReportService reviewReportService,
            ILanguageService lang)
            : base(lang)
        {
            _reviewService = reviewService;
            _reviewReportService = reviewReportService;
        }
        /// <summary>Gets a paginated list of approved reviews for a product.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpGet("product/{productId}")]
        public async Task<IActionResult> GetProductReviews(
                int productId,
                [FromQuery] int pageNumber = 1,
                [FromQuery] int pageSize = 10)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1 || pageSize > 50) pageSize = 10;

            var result = await _reviewService.GetProductReviewsAsync(productId, pageNumber, pageSize);
            return OkResult(data: result, count: result.TotalCount);
        }
        /// <summary>Gets a paginated list of reviews written by the authenticated user.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [HttpGet("my-reviews")]
        [Authorize]
        public async Task<IActionResult> GetMyReviews(
                 [FromQuery] int pageNumber = 1,
                 [FromQuery] int pageSize = 10)
        {
            var userId = GetRequiredUserId();
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1 || pageSize > 50) pageSize = 10;

            var result = await _reviewService.GetUserReviewsAsync(userId, pageNumber, pageSize);
            return OkResult(data: result, count: result.TotalCount);
        }
        /// <summary>Creates a new review for a product.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateReview([FromBody] CreateReviewDto dto)
        {
            var userId = GetRequiredUserId();
            var review = await _reviewService.CreateReviewAsync(userId, dto);
            return CreatedResult(data: review, message: Lang.GetMessage(LangKeys.Review.Submitted));
        }
        /// <summary>Deletes a review owned by the authenticated user.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpDelete("{reviewId}")]
        [Authorize]
        public async Task<IActionResult> DeleteReview(int reviewId)
        {
            var userId = GetRequiredUserId();
            var deleted = await _reviewService.DeleteReviewAsync(reviewId, userId);
            if (!deleted)
                throw new Graduation.BLL.Errors.NotFoundException(Lang.GetMessage(LangKeys.Review.NotFound));

            return OkResult(message: Lang.GetMessage(LangKeys.Review.Deleted));
        }
        /// <summary>Reports a review for administrative moderation.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [HttpPost("report")]
        [Authorize]
        public async Task<IActionResult> ReportReview([FromBody] CreateReviewReportDto dto)
        {
            var userId = GetRequiredUserId();
            var report = await _reviewReportService.CreateReportAsync(userId, dto);
            return CreatedResult(data: report, message: Lang.GetMessage(LangKeys.Review.Reported));
        }
    }
}
