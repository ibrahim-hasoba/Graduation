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

                _logger.LogInformation("Webhook POST keys: {Keys}",
                    string.Join(",", callbackData.Keys));

                callbackData.TryGetValue("hmac", out var hmac);
                callbackData.Remove("hmac");

                if (string.IsNullOrEmpty(hmac))
                {
                    _logger.LogWarning("Webhook POST received without HMAC — ignored");
                    return Ok();
                }

                await _paymentService.HandleWebhookAsync(callbackData, hmac);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Webhook POST error");
                return Ok();
            }
        }

       

        [HttpGet("webhook")]
        [AllowAnonymous]
        public IActionResult WebhookGet()
        {
            try
            {
                Request.Query.TryGetValue("success", out var success);
                Request.Query.TryGetValue("merchant_order_id", out var orderNumber);
                Request.Query.TryGetValue("client_type", out var clientType);

                _logger.LogInformation(
                    "Webhook GET: order={Order}, success={Success}, client={Client}",
                    orderNumber, success, clientType);

                var isSuccess = string.Equals(
                    success, "true", StringComparison.OrdinalIgnoreCase);
                var isMobile = string.Equals(
                    clientType, "mobile", StringComparison.OrdinalIgnoreCase);

                string redirectUrl;

                if (isMobile)
                {
                    redirectUrl = isSuccess
                        ? $"heka://payment-success?order={orderNumber}"
                        : $"heka://payment-failed?order={orderNumber}";
                }
                else
                {
                    redirectUrl = isSuccess
                        ? $"https://heka-eg.netlify.app/payment-success?order={orderNumber}"
                        : $"https://heka-eg.netlify.app/payment-failed?order={orderNumber}";
                }

                return Redirect(redirectUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Webhook GET error");
                return Redirect("https://heka-eg.netlify.app/payment-failed");
            }
        }

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