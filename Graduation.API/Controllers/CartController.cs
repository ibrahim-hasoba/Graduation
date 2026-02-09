using Graduation.API.Errors;
using Graduation.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shared.DTOs.Cart;

namespace Graduation.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CartController : ControllerBase
    {
        private readonly ICartService _cartService;

        public CartController(ICartService cartService)
        {
            _cartService = cartService;
        }

        /// <summary>
        /// Get user's shopping cart
        /// </summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetCart()
        {
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            var cart = await _cartService.GetUserCartAsync(userId);
            return Ok(new { success = true, data = cart });
        }

        /// <summary>
        /// Get cart items count
        /// </summary>
        [HttpGet("count")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetCartCount()
        {
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            var count = await _cartService.GetCartItemsCountAsync(userId);
            return Ok(new { success = true, data = new { count } });
        }

        /// <summary>
        /// Add item to cart
        /// </summary>
        [HttpPost("items")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> AddToCart([FromBody] AddToCartDto dto)
        {
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            var cartItem = await _cartService.AddToCartAsync(userId, dto);
            return StatusCode(201, new
            {
                success = true,
                message = "Item added to cart successfully",
                data = cartItem
            });
        }

        /// <summary>
        /// Update cart item quantity
        /// </summary>
        [HttpPut("items/{cartItemId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateCartItem(int cartItemId, [FromBody] UpdateCartItemDto dto)
        {
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            var cartItem = await _cartService.UpdateCartItemAsync(userId, cartItemId, dto);
            return Ok(new
            {
                success = true,
                message = "Cart item updated successfully",
                data = cartItem
            });
        }

        /// <summary>
        /// Remove item from cart
        /// </summary>
        [HttpDelete("items/{cartItemId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RemoveFromCart(int cartItemId)
        {
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            await _cartService.RemoveFromCartAsync(userId, cartItemId);
            return Ok(new
            {
                success = true,
                message = "Item removed from cart successfully"
            });
        }

        /// <summary>
        /// Clear entire cart
        /// </summary>
        [HttpDelete]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ClearCart()
        {
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            await _cartService.ClearCartAsync(userId);
            return Ok(new
            {
                success = true,
                message = "Cart cleared successfully"
            });
        }
    }
}
