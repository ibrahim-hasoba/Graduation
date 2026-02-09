using Graduation.API.Errors;
using Graduation.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
      var userId = User.FindFirst("userId")?.Value;
      if (string.IsNullOrEmpty(userId))
        return Unauthorized(new { success = false, message = "User not authenticated" });

      try
      {
        var wishlist = await _wishlistService.GetUserWishlistAsync(userId);
        return Ok(new { success = true, count = wishlist.Count, data = wishlist });
      }
      catch (Exception ex)
      {
        return BadRequest(new { success = false, message = ex.Message });
      }
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
      var userId = User.FindFirst("userId")?.Value;
      if (string.IsNullOrEmpty(userId))
        return Unauthorized(new { success = false, message = "User not authenticated" });

      try
      {
        var wishlistItem = await _wishlistService.AddToWishlistAsync(userId, dto.ProductId);
        return StatusCode(201, new { success = true, message = "Product added to wishlist", data = wishlistItem });
      }
      catch (NotFoundException ex)
      {
        return NotFound(new { success = false, message = ex.Message });
      }
      catch (ConflictException ex)
      {
        return Conflict(new { success = false, message = ex.Message });
      }
      catch (Exception ex)
      {
        return BadRequest(new { success = false, message = ex.Message });
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
      var userId = User.FindFirst("userId")?.Value;
      if (string.IsNullOrEmpty(userId))
        return Unauthorized(new { success = false, message = "User not authenticated" });

      try
      {
        await _wishlistService.RemoveFromWishlistAsync(userId, productId);
        return Ok(new { success = true, message = "Product removed from wishlist" });
      }
      catch (NotFoundException ex)
      {
        return NotFound(new { success = false, message = ex.Message });
      }
      catch (Exception ex)
      {
        return BadRequest(new { success = false, message = ex.Message });
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
      var userId = User.FindFirst("userId")?.Value;
      if (string.IsNullOrEmpty(userId))
        return Unauthorized(new { success = false, message = "User not authenticated" });

      try
      {
        var isInWishlist = await _wishlistService.IsInWishlistAsync(userId, productId);
        return Ok(new { success = true, isInWishlist });
      }
      catch (Exception ex)
      {
        return BadRequest(new { success = false, message = ex.Message });
      }
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
      var userId = User.FindFirst("userId")?.Value;
      if (string.IsNullOrEmpty(userId))
        return Unauthorized(new { success = false, message = "User not authenticated" });

      try
      {
        await _wishlistService.ClearWishlistAsync(userId);
        return Ok(new { success = true, message = "Wishlist cleared" });
      }
      catch (Exception ex)
      {
        return BadRequest(new { success = false, message = ex.Message });
      }
    }
  }
}
