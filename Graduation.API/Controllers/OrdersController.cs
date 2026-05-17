using Graduation.API.Extensions;
using Graduation.BLL.Services.Implementations;
using Graduation.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.DTOs.Order;

namespace Graduation.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class OrdersController : BaseController
    {
        private readonly IOrderService _orderService;
        private readonly IVendorService _vendorService;
        private readonly IPaymentService _paymentService;

        public OrdersController(
            IOrderService orderService,
            IVendorService vendorService,
            IPaymentService paymentService,
            ILanguageService lang)
            : base(lang)
        {
            _orderService = orderService;
            _vendorService = vendorService;
            _paymentService = paymentService;
        }
        /// <summary>Creates a new order from the authenticated user's cart.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDto dto)
        {
            var userId = GetRequiredUserId();
            var order = await _orderService.CreateOrderAsync(userId, dto);
            return CreatedResult(data: order, message: Lang.GetMessage(LangKeys.Order.Placed));
        }
        /// <summary>Initiates a payment session for an existing order.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPost("{id}/initiate-payment")]
        public async Task<IActionResult> InitiatePayment(int id)
        {
            var userId = GetRequiredUserId();
            var result = await _paymentService.InitiatePaymentAsync(id);
            return OkResult(data: result);
        }
        /// <summary>Gets the payment status for a specific order by order number.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpGet("{orderNumber}/payment-status")]
        public async Task<IActionResult> GetPaymentStatus(string orderNumber)
        {
            var userId = GetRequiredUserId();
            var payment = await _paymentService.GetByOrderNumberAsync(orderNumber, userId);

            return OkResult(data: new
            {
                orderNumber,
                paymentStatus = payment.Status,
                paymentMethod = payment.Method,
                amount = payment.Amount,
                paidAt = payment.PaidAt,
                isTerminal = IsTerminalStatus(payment.Status)
            });
        }
        /// <summary>Gets a paginated list of orders for the authenticated user.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet("my-orders")]
        public async Task<IActionResult> GetMyOrders([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var userId = GetRequiredUserId();
            var pagedOrders = await _orderService.GetUserOrdersAsync(userId, pageNumber, pageSize);
            return OkResult(data: pagedOrders);
        }
        /// <summary>Gets a single order's details by its ID.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetOrderById(int id)
        {
            var userId = GetRequiredUserId();
            var order = await _orderService.GetOrderByIdAsync(id, userId);
            return OkResult(data: order);
        }
        /// <summary>Gets all orders containing the authenticated vendor's products.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet("vendor")]
        public async Task<IActionResult> GetVendorOrders()
        {
            var userId = GetRequiredUserId();
            var vendor = await _vendorService.GetVendorByUserIdAsync(userId);
            if (vendor == null)
                throw new Shared.Errors.UnauthorizedException(Lang.GetMessage(LangKeys.Order.NotVendor));

            var orders = await _orderService.GetVendorOrdersAsync(vendor.Id);
            return OkResult(data: orders);
        }
        /// <summary>Updates the status of an order for the vendor's products.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusDto dto)
        {
            var userId = GetRequiredUserId();
            var vendor = await _vendorService.GetVendorByUserIdAsync(userId);
            if (vendor == null)
                throw new Shared.Errors.UnauthorizedException(Lang.GetMessage(LangKeys.Order.UpdateNotVendor));

            var order = await _orderService.UpdateOrderStatusAsync(id, vendor.Id, dto);
            return OkResult(data: order, message: Lang.GetMessage(LangKeys.Order.StatusUpdated));
        }
        /// <summary>Cancels an order with an optional reason.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPost("{id}/cancel")]
        public async Task<IActionResult> CancelOrder(int id, [FromBody] CancelOrderDto dto)
        {
            var userId = GetRequiredUserId();
            var order = await _orderService.CancelOrderAsync(
                id, userId, dto.Reason ?? "Cancelled by customer");

            return OkResult(data: order, message: Lang.GetMessage(LangKeys.Order.Cancelled));
        }
        /// <summary>Gets map tracking data for a shipped order.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpGet("{orderNumber}/track")]
        public async Task<IActionResult> GetOrderTracking(string orderNumber)
        {
            var userId = GetRequiredUserId();
            var trackingData = await _orderService.GetOrderMapTrackingAsync(orderNumber, userId);
            return OkResult(data: trackingData);
        }

        private static bool IsTerminalStatus(string status) =>
            status is "Paid" or "Failed" or "Refunded";
    }

    public class CancelOrderDto
    {
        public string? Reason { get; set; }
    }
}
