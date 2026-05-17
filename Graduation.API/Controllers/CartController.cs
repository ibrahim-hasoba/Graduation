using Graduation.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Graduation.API.Extensions;
using Shared.DTOs.Cart;

namespace Graduation.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CartController : BaseController
    {
        private readonly ICartService _cartService;

        public CartController(ICartService cartService, ILanguageService lang)
            : base(lang)
        {
            _cartService = cartService;
        }
        /// <summary>Gets the authenticated user's shopping cart with all items.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet]
        public async Task<IActionResult> GetCart()
        {
            var userId = GetRequiredUserId();
            var cart = await _cartService.GetUserCartAsync(userId);
            return OkResult(data: cart);
        }
        /// <summary>Gets the total number of items in the authenticated user's cart.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet("count")]
        public async Task<IActionResult> GetCartCount()
        {
            var userId = GetRequiredUserId();
            var count = await _cartService.GetCartItemsCountAsync(userId);
            return OkResult(data: new { count });
        }
        /// <summary>Adds a product to the authenticated user's cart with optional quantity and variant.</summary>

        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpPost("items")]
        public async Task<IActionResult> AddToCart([FromBody] AddToCartDto dto)
        {
            var userId = GetRequiredUserId();
            var cartItem = await _cartService.AddToCartAsync(userId, dto);
            return CreatedResult(data: cartItem, message: Lang.GetMessage(LangKeys.Cart.ItemAdded));
        }
        /// <summary>Updates the quantity or variant selection of an existing cart item.</summary>
        /// <summary>Updates the quantity or variant selection of an existing cart item.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPut("items/{cartItemId}")]
        public async Task<IActionResult> UpdateCartItem(int cartItemId, [FromBody] UpdateCartItemDto dto)
        {
            var userId = GetRequiredUserId();
            var cartItem = await _cartService.UpdateCartItemAsync(userId, cartItemId, dto);
            return OkResult(data: cartItem, message: Lang.GetMessage(LangKeys.Cart.ItemUpdated));
        }
        /// <summary>Removes a specific item from the authenticated user's cart.</summary>
        /// <summary>Removes a specific item from the authenticated user's cart.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpDelete("items/{cartItemId}")]
        public async Task<IActionResult> RemoveFromCart(int cartItemId)
        {
            var userId = GetRequiredUserId();
            await _cartService.RemoveFromCartAsync(userId, cartItemId);
            return OkResult(message: Lang.GetMessage(LangKeys.Cart.ItemRemoved));
        }
        /// <summary>Removes all items from the authenticated user's cart.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpDelete]
        public async Task<IActionResult> ClearCart()
        {
            var userId = GetRequiredUserId();
            await _cartService.ClearCartAsync(userId);
            return OkResult(message: Lang.GetMessage(LangKeys.Cart.Cleared));
        }
    }
}
