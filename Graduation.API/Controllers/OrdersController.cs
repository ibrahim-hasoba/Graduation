using Graduation.API.Errors;
using Graduation.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            var order = await _orderService.CreateOrderAsync(userId, dto);

            return StatusCode(201, new
            {
                success = true,
                message = "Order placed successfully!",
                data = order
            });
        }

        /// <summary>
        /// Get user's orders
        /// </summary>
        [HttpGet("my-orders")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMyOrders()
        {
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            var orders = await _orderService.GetUserOrdersAsync(userId);
            return Ok(new { success = true, data = orders });
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
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            var order = await _orderService.GetOrderByIdAsync(id, userId);
            return Ok(new { success = true, data = order });
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
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            var vendor = await _vendorService.GetVendorByUserIdAsync(userId);
            if (vendor == null)
                throw new UnauthorizedException("You must be a vendor to view vendor orders");

            var orders = await _orderService.GetVendorOrdersAsync(vendor.Id);
            return Ok(new { success = true, data = orders });
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
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            var vendor = await _vendorService.GetVendorByUserIdAsync(userId);
            if (vendor == null)
                throw new UnauthorizedException("You must be a vendor to update order status");

            var order = await _orderService.UpdateOrderStatusAsync(id, vendor.Id, dto);

            return Ok(new
            {
                success = true,
                message = "Order status updated successfully",
                data = order
            });
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
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            var order = await _orderService.CancelOrderAsync(id, userId, dto.Reason ?? "Cancelled by customer");

            return Ok(new
            {
                success = true,
                message = "Order cancelled successfully",
                data = order
            });
        }
    }

    public class CancelOrderDto
    {
        public string? Reason { get; set; }
    }
}
