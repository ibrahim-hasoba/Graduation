using Graduation.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Graduation.API.Controllers
{
    [Route("api/admin/review-reports")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminReviewReportsController : BaseController
    {
        private readonly IReviewReportService _reviewReportService;
        private readonly IActivityLogService _activityLog;

        public AdminReviewReportsController(
            IReviewReportService reviewReportService,
            IActivityLogService activityLog,
            ILanguageService lang)
            : base(lang)
        {
            _reviewReportService = reviewReportService;
            _activityLog = activityLog;
        }
        /// <summary>Gets a paginated list of pending review reports.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet]
        public async Task<IActionResult> GetPendingReviewReports(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _reviewReportService.GetPendingReportsAsync(pageNumber, pageSize);
            return OkResult(data: result, count: result.TotalCount);
        }
        /// <summary>Gets a single review report by its ID.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpGet("{reportId}")]
        public async Task<IActionResult> GetReviewReport(int reportId)
        {
            var report = await _reviewReportService.GetReportByIdAsync(reportId);
            if (report == null)
                throw new Shared.Errors.NotFoundException(Lang.GetMessage(LangKeys.Review.NotFoundSimple), reportId);
            return OkResult(data: report);
        }
        /// <summary>Approves a review report and takes action on the reported review.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPost("{reportId}/approve")]
        public async Task<IActionResult> ApproveReviewReport(int reportId)
        {
            var adminId = GetRequiredUserId();
            var result = await _reviewReportService.ApproveReportAsync(reportId, adminId);
            if (!result)
                throw new Shared.Errors.BadRequestException(Lang.GetMessage(LangKeys.Report.NotFoundOrResolved));
            await _activityLog.LogAsync(adminId, "Approve", "Review", reportId.ToString(), $"Approved review report #{reportId}");
            return OkResult(message: Lang.GetMessage(LangKeys.Report.Approved));
        }
        /// <summary>Dismisses a review report without taking further action.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPost("{reportId}/dismiss")]
        public async Task<IActionResult> DismissReviewReport(int reportId)
        {
            var adminId = GetRequiredUserId();
            var result = await _reviewReportService.DismissReportAsync(reportId, adminId);
            if (!result)
                throw new Shared.Errors.BadRequestException(Lang.GetMessage(LangKeys.Report.NotFoundOrResolved));
            await _activityLog.LogAsync(adminId, "Dismiss", "Review", reportId.ToString(), $"Dismissed review report #{reportId}");
            return OkResult(message: Lang.GetMessage(LangKeys.Report.Dismissed));
        }
        /// <summary>Deletes the reviewed content associated with a review report.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpDelete("{reportId}/review")]
        public async Task<IActionResult> DeleteReviewFromReport(int reportId)
        {
            var adminId = GetRequiredUserId();
            var result = await _reviewReportService.DeleteReviewFromReportAsync(reportId, adminId);
            if (!result)
                throw new Shared.Errors.NotFoundException(Lang.GetMessage(LangKeys.Review.NotFoundSimple), reportId);
            await _activityLog.LogAsync(adminId, "Delete", "Review", reportId.ToString(), $"Deleted review from report #{reportId}");
            return OkResult(message: Lang.GetMessage(LangKeys.Report.ReviewDeleted));
        }
    }
}
