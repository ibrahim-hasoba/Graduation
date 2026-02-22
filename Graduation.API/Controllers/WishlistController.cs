using Graduation.API.Errors;
using Graduation.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Graduation.API.Extensions;
using Shared.DTOs.Wishlist;
using System.Security.Claims;

namespace Graduation.API.Controllers
{
  [Route("api/[controller]")]
  [ApiController]
  [Authorize]
  public class WishlistController : ControllerBase
  {
    private readonly IWishlistService _wishlistService;

    public WishlistController(IWishlistService wishlistService)
    {
      _wishlistService = wishlistService;
    }

    /// <summary>
    /// Get user's wishlist
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetWishlist()
    {
      var userId = User.GetUserId();
      if (string.IsNullOrEmpty(userId))
        return Unauthorized(new ApiResponse(401, "User not authenticated"));
      var wishlist = await _wishlistService.GetUserWishlistAsync(userId);
      return Ok(new Errors.ApiResult(data: wishlist, count: wishlist.Count));
    }

    /// <summary>
    /// Add product to wishlist
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddToWishlist([FromBody] AddToWishlistDto dto)
    {
      var userId = User.GetUserId();
      if (string.IsNullOrEmpty(userId))
        return Unauthorized(new ApiResponse(401, "User not authenticated"));

      try
      {
        var wishlistItem = await _wishlistService.AddToWishlistAsync(userId, dto.ProductId);
        return StatusCode(201, new Errors.ApiResult(data: wishlistItem, message: "Product added to wishlist"));
      }
      catch (NotFoundException ex)
      {
        return NotFound(new ApiResponse(404, ex.Message));
      }
      catch (ConflictException ex)
      {
        return Conflict(new ApiResponse(409, ex.Message));
      }
    }

    /// <summary>
    /// Remove product from wishlist
    /// </summary>
    [HttpDelete("{productId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveFromWishlist(int productId)
    {
      var userId = User.GetUserId();
      if (string.IsNullOrEmpty(userId))
        return Unauthorized(new ApiResponse(401, "User not authenticated"));

      try
      {
        await _wishlistService.RemoveFromWishlistAsync(userId, productId);
        return Ok(new Errors.ApiResult(message: "Product removed from wishlist"));
      }
      catch (NotFoundException ex)
      {
        return NotFound(new ApiResponse(404, ex.Message));
      }
    }

    /// <summary>
    /// Check if product is in wishlist
    /// </summary>
    [HttpGet("{productId}/check")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CheckWishlist(int productId)
    {
      var userId = User.GetUserId();
      if (string.IsNullOrEmpty(userId))
        return Unauthorized(new ApiResponse(401, "User not authenticated"));
      var isInWishlist = await _wishlistService.IsInWishlistAsync(userId, productId);
      return Ok(new Errors.ApiResult(data: new { isInWishlist }));
    }

    /// <summary>
    /// Clear user's entire wishlist
    /// </summary>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ClearWishlist()
    {
      var userId = User.GetUserId();
      if (string.IsNullOrEmpty(userId))
        return Unauthorized(new ApiResponse(401, "User not authenticated"));
      await _wishlistService.ClearWishlistAsync(userId);
      return Ok(new Errors.ApiResult(message: "Wishlist cleared"));
    }
  }
}
