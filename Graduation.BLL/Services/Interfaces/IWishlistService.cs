using Shared.DTOs.Wishlist;

namespace Graduation.BLL.Services.Interfaces
{
  public interface IWishlistService
  {
    Task<WishlistDto> AddToWishlistAsync(string userId, int productId);
    Task RemoveFromWishlistAsync(string userId, int productId);
    Task<List<WishlistDto>> GetUserWishlistAsync(string userId);
    Task<bool> IsInWishlistAsync(string userId, int productId);
    Task ClearWishlistAsync(string userId);
  }
}
