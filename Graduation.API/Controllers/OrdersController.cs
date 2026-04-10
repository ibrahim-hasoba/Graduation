using Graduation.API.Extensions;
using Graduation.BLL.Services.Implementations;
using Graduation.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shared.DTOs.Order;
using Shared.Errors;

namespace Graduation.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly IVendorService _vendorService;
        private readonly IPaymentService _paymentService;

        public OrdersController(
            IOrderService orderService,
            IVendorService vendorService,
            IPaymentService paymentService)
        {
            _orderService = orderService;
            _vendorService = vendorService;
            _paymentService = paymentService;
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDto dto)
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            var order = await _orderService.CreateOrderAsync(userId, dto);
            return StatusCode(201, new Errors.ApiResult(data: order, message: "Order placed successfully!"));
        }

        [HttpPost("{id}/initiate-payment")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> InitiatePayment(int id)
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            var order = await _orderService.GetOrderByIdAsync(id, userId);

            var result = await _paymentService.InitiatePaymentAsync(id);
            return Ok(new Errors.ApiResult(data: result));
        }

        
        [HttpGet("{orderNumber}/payment-status")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetPaymentStatus(string orderNumber)
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            var payment = await _paymentService.GetByOrderNumberAsync(orderNumber, userId);

            return Ok(new Errors.ApiResult(data: new
            {
                orderNumber,
                paymentStatus = payment.Status,
                paymentMethod = payment.Method,
                amount = payment.Amount,
                paidAt = payment.PaidAt,
                isTerminal = IsTerminalStatus(payment.Status)
            }));
        }

        

        [HttpGet("my-orders")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMyOrders([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            
            var pagedOrders = await _orderService.GetUserOrdersAsync(userId, pageNumber, pageSize);

            return Ok(new Errors.ApiResult(data: pagedOrders));
        }

        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetOrderById(int id)
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            var order = await _orderService.GetOrderByIdAsync(id, userId);
            return Ok(new Errors.ApiResult(data: order));
        }

        [HttpGet("vendor")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetVendorOrders()
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            var vendor = await _vendorService.GetVendorByUserIdAsync(userId);
            if (vendor == null)
                throw new UnauthorizedException("You must be a vendor to view vendor orders");

            var orders = await _orderService.GetVendorOrdersAsync(vendor.Id);
            return Ok(new Errors.ApiResult(data: orders));
        }

        [HttpPatch("{id}/status")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusDto dto)
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            var vendor = await _vendorService.GetVendorByUserIdAsync(userId);
            if (vendor == null)
                throw new UnauthorizedException("You must be a vendor to update order status");

            var order = await _orderService.UpdateOrderStatusAsync(id, vendor.Id, dto);
            return Ok(new Errors.ApiResult(data: order, message: "Order status updated successfully"));
        }

        [HttpPost("{id}/cancel")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CancelOrder(int id, [FromBody] CancelOrderDto dto)
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            var order = await _orderService.CancelOrderAsync(
                id, userId, dto.Reason ?? "Cancelled by customer");

            return Ok(new Errors.ApiResult(data: order, message: "Order cancelled successfully"));
        }

        // ── Helper ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Terminal statuses mean no further change is expected —
        /// the frontend can stop polling once it sees one of these.
        /// </summary>
        private static bool IsTerminalStatus(string status) =>
            status is "Paid" or "Failed" or "Refunded";
    }

    public class CancelOrderDto
    {
        public string? Reason { get; set; }
    }
}
