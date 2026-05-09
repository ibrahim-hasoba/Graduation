using Graduation.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Graduation.API.Extensions;
using Shared.DTOs.Cart;
using Shared.Errors;

namespace Graduation.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CartController : ControllerBase
    {
        private readonly ICartService _cartService;
        private readonly ILanguageService _lang;

        public CartController(ICartService cartService, ILanguageService lang)
        {
            _cartService = cartService;
            _lang = lang;
        }

        /// <summary>
        /// Get user's shopping cart
        /// </summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetCart()
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, _lang.GetMessage("NotAuthenticated")));

            var cart = await _cartService.GetUserCartAsync(userId);
            return Ok(new ApiResult(data: cart));
        }

        /// <summary>
        /// Get cart items count
        /// </summary>
        [HttpGet("count")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetCartCount()
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, _lang.GetMessage("NotAuthenticated")));

            var count = await _cartService.GetCartItemsCountAsync(userId);
            return Ok(new ApiResult(data: new { count }));
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
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, _lang.GetMessage("NotAuthenticated")));

            var cartItem = await _cartService.AddToCartAsync(userId, dto);
            return StatusCode(201, new ApiResult(data: cartItem, message: _lang.GetMessage("Cart_ItemAdded")));
        }

        /// <summary>
        /// Update a cart item's quantity and/or selected variants
        /// </summary>
        [HttpPut("items/{cartItemId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateCartItem(int cartItemId, [FromBody] UpdateCartItemDto dto)
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, _lang.GetMessage("NotAuthenticated")));

            var cartItem = await _cartService.UpdateCartItemAsync(userId, cartItemId, dto);

            return Ok(new ApiResult(data: cartItem, message: _lang.GetMessage("Cart_ItemUpdated")));
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
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, _lang.GetMessage("NotAuthenticated")));

            await _cartService.RemoveFromCartAsync(userId, cartItemId);
            return Ok(new ApiResult(message: _lang.GetMessage("Cart_ItemRemoved")));
        }

        /// <summary>
        /// Clear entire cart
        /// </summary>
        [HttpDelete]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ClearCart()
        {
            // FIXED BUG: Was using User.FindFirst("userId")?.Value which only checks the
            // custom "userId" claim and misses the standard ClaimTypes.NameIdentifier claim,
            // causing 401 for users whose JWT was issued with the standard claim type.
            // Now uses the GetUserId() extension which checks both claim types consistently.
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, _lang.GetMessage("NotAuthenticated")));

            await _cartService.ClearCartAsync(userId);
            return Ok(new ApiResult(message: _lang.GetMessage("Cart_Cleared")));
        }
    }
}
