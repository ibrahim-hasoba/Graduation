using Graduation.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Errors;

namespace Graduation.API.Controllers
{
    [Route("api/admin/reports")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminReportsController : BaseController
    {
        private readonly IReportService _reportService;
        private readonly ICodeLookupService _codeLookup;

        public AdminReportsController(
            IReportService reportService,
            ICodeLookupService codeLookup,
            ILanguageService lang)
            : base(lang)
        {
            _reportService = reportService;
            _codeLookup = codeLookup;
        }

        /// <summary>Gets a sales report for a specified date range, optionally filtered by vendor.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet("sales")]
        public async Task<IActionResult> GetSalesReport(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate,
            [FromQuery] string? vendorCode = null)
        {
            if (startDate == DateTime.MinValue || endDate == DateTime.MinValue)
                return BadRequest(new Shared.Errors.ApiResponse(400, Lang.GetMessage(LangKeys.Report.DateRangeRequired)));

            int? vendorId = null;
            if (!string.IsNullOrEmpty(vendorCode))
                vendorId = await _codeLookup.ResolveVendorIdAsync(vendorCode);

            var report = await _reportService.GetSalesReportAsync(startDate, endDate, vendorId);
            return OkResult(data: report);
        }

        /// <summary>Gets sales data grouped by category within a date range.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet("sales-by-category")]
        public async Task<IActionResult> GetSalesByCategory(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate)
        {
            if (startDate == DateTime.MinValue || endDate == DateTime.MinValue)
                return BadRequest(new Shared.Errors.ApiResponse(400, Lang.GetMessage(LangKeys.Report.DateRangeRequired)));

            var report = await _reportService.GetSalesByCategoryAsync(startDate, endDate);
            return OkResult(data: report);
        }

        /// <summary>Gets a performance report for a specific vendor.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpGet("vendor-performance/{vendorCode}")]
        public async Task<IActionResult> GetVendorPerformance(string vendorCode)
        {
            var vendorId = await _codeLookup.ResolveVendorIdAsync(vendorCode);
            var report = await _reportService.GetVendorPerformanceAsync(vendorId);
            return OkResult(data: report);
        }

        /// <summary>Gets customer insights and analytics.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet("customer-insights")]
        public async Task<IActionResult> GetCustomerInsights()
        {
            var report = await _reportService.GetCustomerInsightsAsync();
            return OkResult(data: report);
        }

        /// <summary>Gets products with stock below a specified threshold.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet("low-stock")]
        public async Task<IActionResult> GetLowStockProducts([FromQuery] int threshold = 10)
        {
            var report = await _reportService.GetLowStockProductsAsync(threshold);
            return OkResult(data: report);
        }

        /// <summary>Gets revenue data grouped by vendor within a date range.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet("revenue-by-vendor")]
        public async Task<IActionResult> GetRevenueByVendor(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate,
            [FromQuery] int take = 10)
        {
            if (startDate == DateTime.MinValue || endDate == DateTime.MinValue)
                return BadRequest(new Shared.Errors.ApiResponse(400, Lang.GetMessage(LangKeys.Report.DateRangeRequired)));

            var report = await _reportService.GetRevenueByVendorAsync(startDate, endDate, take);
            return OkResult(data: report);
        }

        /// <summary>Gets the top-selling products within a date range.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet("top-products")]
        public async Task<IActionResult> GetTopProductsReport(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate,
            [FromQuery] int take = 10)
        {
            if (startDate == DateTime.MinValue || endDate == DateTime.MinValue)
                return BadRequest(new Shared.Errors.ApiResponse(400, Lang.GetMessage(LangKeys.Report.DateRangeRequired)));

            var report = await _reportService.GetTopProductsAsync(startDate, endDate, take);
            return OkResult(data: report);
        }

        /// <summary>Gets a summary of orders grouped by their current status.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet("order-status-summary")]
        public async Task<IActionResult> GetOrderStatusSummary()
        {
            var report = await _reportService.GetOrderStatusSummaryAsync();
            return OkResult(data: report);
        }

        /// <summary>Gets user registration and growth trends.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet("user-trends")]
        public async Task<IActionResult> GetUserTrends()
        {
            var report = await _reportService.GetUserTrendsAsync();
            return OkResult(data: report);
        }
    }
}
