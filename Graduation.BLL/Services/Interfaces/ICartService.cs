using Shared.DTOs.Cart;
using System;
using System.Collections.Generic;
using System.Text;

namespace Graduation.BLL.Services.Interfaces
{
    public interface ICartService
    {
        Task<CartDto> GetUserCartAsync(string userId);
        Task<CartItemDto> AddToCartAsync(string userId, AddToCartDto dto);
        Task<CartItemDto> UpdateCartItemAsync(string userId, int cartItemId, UpdateCartItemDto dto);
        Task RemoveFromCartAsync(string userId, int cartItemId);
        Task ClearCartAsync(string userId);
        Task<int> GetCartItemsCountAsync(string userId);
    }
}
