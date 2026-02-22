using Graduation.API.Errors;
using Graduation.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Graduation.API.Extensions;
using Shared.DTOs.Order;

namespace Graduation.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly IVendorService _vendorService;

        public OrdersController(IOrderService orderService, IVendorService vendorService)
        {
            _orderService = orderService;
            _vendorService = vendorService;
        }

        /// <summary>
        /// Create new order from cart
        /// </summary>
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

        /// <summary>
        /// Get user's orders
        /// </summary>
        [HttpGet("my-orders")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMyOrders()
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            var orders = await _orderService.GetUserOrdersAsync(userId);
            return Ok(new Errors.ApiResult(data: orders));
        }

        /// <summary>
        /// Get order details by ID
        /// </summary>
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

        /// <summary>
        /// Get vendor's orders
        /// </summary>
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

        /// <summary>
        /// Update order status (vendor only)
        /// </summary>
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

        /// <summary>
        /// Cancel order (customer only)
        /// </summary>
        [HttpPost("{id}/cancel")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CancelOrder(int id, [FromBody] CancelOrderDto dto)
        {
            // FIXED BUG: Was using User.FindFirst("userId")?.Value which only checks the
            // custom "userId" claim and misses the standard ClaimTypes.NameIdentifier claim,
            // causing 401 for users whose JWT was issued with the standard claim type.
            // Now uses the GetUserId() extension which checks both claim types consistently.
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            var order = await _orderService.CancelOrderAsync(id, userId, dto.Reason ?? "Cancelled by customer");

            return Ok(new Errors.ApiResult(data: order, message: "Order cancelled successfully"));
        }
    }

    public class CancelOrderDto
    {
        public string? Reason { get; set; }
    }
}
