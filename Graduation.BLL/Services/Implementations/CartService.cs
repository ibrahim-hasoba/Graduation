using Shared.Errors;
using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs.Cart;

namespace Graduation.BLL.Services.Implementations
{
    public class CartService : ICartService
    {
        private readonly DatabaseContext _context;

        public CartService(DatabaseContext context)
        {
            _context = context;
        }

        public async Task<CartDto> GetUserCartAsync(string userId)
        {
            var cartItems = await _context.CartItems
                .Include(ci => ci.Product)
                    .ThenInclude(p => p.Images)
                .Include(ci => ci.Product.Vendor)
                .Where(ci => ci.UserId == userId)
                .OrderByDescending(ci => ci.AddedAt)
                .ToListAsync();

            var items = cartItems.Select(MapToDto).ToList();
            var subTotal = items.Sum(i => i.TotalPrice);
            var shippingCost = items.Any() ? 30m : 0m;

            return new CartDto
            {
                Items = items,
                TotalItems = items.Sum(i => i.Quantity),
                SubTotal = subTotal,
                ShippingCost = shippingCost,
                TotalAmount = subTotal + shippingCost,
                HasOutOfStockItems = items.Any(i => !i.InStock)
            };
        }

        public async Task<CartItemDto> AddToCartAsync(string userId, AddToCartDto dto)
        {
            var product = await _context.Products
                .Include(p => p.Images)
                .Include(p => p.Vendor)
                .FirstOrDefaultAsync(p => p.Id == dto.ProductId);

            if (product == null)
                throw new NotFoundException("Product", dto.ProductId);

            if (!product.IsActive)
                throw new BadRequestException("This product is no longer available");

            // FIX #13: Reject products from unapproved or suspended vendors.
            if (!product.Vendor.IsApproved || !product.Vendor.IsActive)
                throw new BadRequestException("This product's vendor is currently unavailable");

            if (product.StockQuantity < dto.Quantity)
                throw new BadRequestException($"Only {product.StockQuantity} items available in stock");

            var existingItem = await _context.CartItems
                .Include(ci => ci.Product)
                    .ThenInclude(p => p.Images)
                .Include(ci => ci.Product.Vendor)
                .FirstOrDefaultAsync(ci => ci.UserId == userId && ci.ProductId == dto.ProductId);

            if (existingItem != null)
            {
                var newQuantity = existingItem.Quantity + dto.Quantity;

                if (product.StockQuantity < newQuantity)
                    throw new BadRequestException($"Cannot add more. Only {product.StockQuantity} items available");

                existingItem.Quantity = newQuantity;
                await _context.SaveChangesAsync();
                return MapToDto(existingItem);
            }

            var cartItem = new CartItem
            {
                UserId = userId,
                ProductId = dto.ProductId,
                Quantity = dto.Quantity,
                AddedAt = DateTime.UtcNow
            };

            _context.CartItems.Add(cartItem);
            await _context.SaveChangesAsync();

            cartItem = await _context.CartItems
                .Include(ci => ci.Product)
                    .ThenInclude(p => p.Images)
                .Include(ci => ci.Product.Vendor)
                .FirstAsync(ci => ci.Id == cartItem.Id);

            return MapToDto(cartItem);
        }

        public async Task<CartItemDto> UpdateCartItemAsync(string userId, int cartItemId, UpdateCartItemDto dto)
        {
            var cartItem = await _context.CartItems
                .Include(ci => ci.Product)
                    .ThenInclude(p => p.Images)
                .Include(ci => ci.Product.Vendor)
                .FirstOrDefaultAsync(ci => ci.Id == cartItemId && ci.UserId == userId);

            if (cartItem == null)
                throw new NotFoundException("Cart item not found");

            if (cartItem.Product.StockQuantity < dto.Quantity)
                throw new BadRequestException($"Only {cartItem.Product.StockQuantity} items available in stock");

            cartItem.Quantity = dto.Quantity;
            await _context.SaveChangesAsync();

            return MapToDto(cartItem);
        }

        public async Task RemoveFromCartAsync(string userId, int cartItemId)
        {
            var cartItem = await _context.CartItems
                .FirstOrDefaultAsync(ci => ci.Id == cartItemId && ci.UserId == userId);

            if (cartItem == null)
                throw new NotFoundException("Cart item not found");

            _context.CartItems.Remove(cartItem);
            await _context.SaveChangesAsync();
        }

        public async Task ClearCartAsync(string userId)
        {
            var cartItems = await _context.CartItems
                .Where(ci => ci.UserId == userId)
                .ToListAsync();

            _context.CartItems.RemoveRange(cartItems);
            await _context.SaveChangesAsync();
        }

        public async Task<int> GetCartItemsCountAsync(string userId)
        {
            return await _context.CartItems
                .Where(ci => ci.UserId == userId)
                .SumAsync(ci => ci.Quantity);
        }

        private CartItemDto MapToDto(CartItem cartItem)
        {
            var unitPrice = cartItem.Product.DiscountPrice ?? cartItem.Product.Price;
            var primaryImage = cartItem.Product.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
                ?? cartItem.Product.Images.FirstOrDefault()?.ImageUrl;

            return new CartItemDto
            {
                Id = cartItem.Id,
                ProductId = cartItem.ProductId,
                ProductNameAr = cartItem.Product.NameAr,
                ProductNameEn = cartItem.Product.NameEn,
                ProductImage = primaryImage,
                Price = cartItem.Product.Price,
                DiscountPrice = cartItem.Product.DiscountPrice,
                UnitPrice = unitPrice,
                Quantity = cartItem.Quantity,
                TotalPrice = unitPrice * cartItem.Quantity,
                StockAvailable = cartItem.Product.StockQuantity,
                InStock = cartItem.Product.StockQuantity >= cartItem.Quantity,
                VendorId = cartItem.Product.VendorId,
                VendorName = cartItem.Product.Vendor.StoreName,
                AddedAt = cartItem.AddedAt
            };
        }
    }
}
