using Graduation.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Graduation.API.Extensions;
using Graduation.BLL.DTOs.Wishlist;

namespace Graduation.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class WishlistController : BaseController
    {
        private readonly IWishlistService _wishlistService;

        public WishlistController(IWishlistService wishlistService, ILanguageService lang)
            : base(lang)
        {
            _wishlistService = wishlistService;
        }
        /// <summary>Gets the authenticated user's wishlist items with pagination.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet]
        public async Task<IActionResult> GetWishlist(
          [FromQuery] int pageNumber = 1,
          [FromQuery] int pageSize = 20)
        {
            var userId = GetRequiredUserId();
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var wishlist = await _wishlistService.GetUserWishlistAsync(userId);

            var totalCount = wishlist.Count;
            var paged = wishlist
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return OkResult(
                data: new
                {
                    items = paged,
                    totalCount,
                    pageNumber,
                    pageSize,
                    totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                },
                count: totalCount);
        }
        /// <summary>Adds a product to the authenticated user's wishlist.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpPost]
        public async Task<IActionResult> AddToWishlist([FromBody] AddToWishlistDto dto)
        {
            var userId = GetRequiredUserId();
            var wishlistItem = await _wishlistService.AddToWishlistAsync(userId, dto.ProductId);
            return CreatedResult(data: wishlistItem, message: Lang.GetMessage(LangKeys.Wishlist.Added));
        }
        /// <summary>Removes a specific product from the authenticated user's wishlist.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpDelete("{productId}")]
        public async Task<IActionResult> RemoveFromWishlist(int productId)
        {
            var userId = GetRequiredUserId();
            await _wishlistService.RemoveFromWishlistAsync(userId, productId);
            return OkResult(message: Lang.GetMessage(LangKeys.Wishlist.Removed));
        }
        /// <summary>Checks if a specific product is in the authenticated user's wishlist.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpGet("{productId}/check")]
        public async Task<IActionResult> CheckWishlist(int productId)
        {
            var userId = GetRequiredUserId();
            var isInWishlist = await _wishlistService.IsInWishlistAsync(userId, productId);
            return OkResult(data: new { isInWishlist });
        }
        /// <summary>Removes all items from the authenticated user's wishlist.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpDelete]
        public async Task<IActionResult> ClearWishlist()
        {
            var userId = GetRequiredUserId();
            await _wishlistService.ClearWishlistAsync(userId);
            return OkResult(message: Lang.GetMessage(LangKeys.Wishlist.Cleared));
        }
    }
}
