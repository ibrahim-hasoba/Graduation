using Shared.Errors;
using Graduation.API.Extensions;
using Graduation.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Graduation.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly ILogger<PaymentsController> _logger;

        public PaymentsController(
            IPaymentService paymentService,
            ILogger<PaymentsController> logger)
        {
            _paymentService = paymentService;
            _logger = logger;
        }

        // ── Get payment by order number (customer) ─────────────────────────────

        [HttpGet("{orderNumber}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetPayment(string orderNumber)
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            var payment = await _paymentService.GetByOrderNumberAsync(orderNumber, userId);
            return Ok(new ApiResult(data: payment));
        }

        // ── Paymob POST webhook (Transaction Processed Callback) ───────────────
        // Paymob sends this server-to-server with JSON body after payment

        [HttpPost("webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> WebhookPost()
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                var body = await reader.ReadToEndAsync();

                Dictionary<string, string> callbackData;

                if (!string.IsNullOrWhiteSpace(body))
                {
                    var jsonDoc = JsonDocument.Parse(body);
                    callbackData = FlattenJson(jsonDoc.RootElement);
                }
                else
                {
                    callbackData = Request.Query
                        .ToDictionary(q => q.Key, q => q.Value.ToString());
                }

                _logger.LogInformation("Webhook keys: {Keys}",
                        string.Join(",", callbackData.Keys));

                callbackData.TryGetValue("hmac", out var hmac);
                callbackData.Remove("hmac");

                if (string.IsNullOrEmpty(hmac))
                {
                    _logger.LogWarning("Webhook received without HMAC");
                    return Ok();
                }

                await _paymentService.HandleWebhookAsync(callbackData, hmac);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Webhook error");
                return Ok();
            }
        }

        // ── Paymob GET webhook (Transaction Response Callback) ─────────────────
        // Paymob redirects the customer's browser here after payment with query params

        [HttpGet("webhook")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> WebhookGet()
        {
            try
            {
                var callbackData = Request.Query
                    .ToDictionary(q => q.Key, q => q.Value.ToString());

                callbackData.TryGetValue("hmac", out var hmac);
                callbackData.Remove("hmac");

                callbackData.TryGetValue("success", out var success);
                callbackData.TryGetValue("merchant_order_id", out var orderNumber);

                _logger.LogInformation(
                    "Paymob GET webhook: order={OrderNumber}, success={Success}",
                    orderNumber, success);

                if (!string.IsNullOrEmpty(hmac))
                    await _paymentService.HandleWebhookAsync(callbackData, hmac);

                // Redirect customer browser to frontend
                var isSuccess = string.Equals(success, "true", StringComparison.OrdinalIgnoreCase);
                var frontendUrl = isSuccess
                    ? $"https://heka-panel.netlify.app/payment-success?order={orderNumber}"
                    : $"https://heka-panel.netlify.app/payment-failed?order={orderNumber}";

                return Redirect(frontendUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Paymob GET webhook");
                return Redirect("https://heka-panel.netlify.app/payment-failed");
            }
        }

        // ── Admin: all payments ────────────────────────────────────────────────

        [HttpGet]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllPayments(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? status = null)
        {
            var result = await _paymentService.GetAllAsync(pageNumber, pageSize, status);

            return Ok(new ApiResult(
                data: new
                {
                    payments = result.Payments,
                    totalCount = result.TotalCount,
                    pageNumber = result.PageNumber,
                    pageSize = result.PageSize,
                    totalPages = result.TotalPages,
                    hasPreviousPage = result.HasPreviousPage,
                    hasNextPage = result.HasNextPage
                },
                count: result.TotalCount));
        }

        // ── Helper: flatten nested JSON to dot-notation dictionary ─────────────

        private static Dictionary<string, string> FlattenJson(
            JsonElement element, string prefix = "")
        {
            var result = new Dictionary<string, string>();

            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    var key = string.IsNullOrEmpty(prefix)
                        ? property.Name
                        : $"{prefix}.{property.Name}";

                    if (property.Value.ValueKind == JsonValueKind.Object ||
                        property.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var nested in FlattenJson(property.Value, key))
                            result[nested.Key] = nested.Value;
                    }
                    else
                    {
                        result[key] = property.Value.ToString();
                    }
                }
            }

            return result;
        }
    }
}
