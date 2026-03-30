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
                .Include(ci => ci.Variant)
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
            if (dto.Quantity <= 0)
                throw new BadRequestException("Quantity must be greater than 0");

            var product = await _context.Products
                .Include(p => p.Images)
                .Include(p => p.Vendor)
                .FirstOrDefaultAsync(p => p.Id == dto.ProductId);

            if (product == null)
                throw new NotFoundException("Product", dto.ProductId);

            if (!product.IsActive)
                throw new BadRequestException("This product is no longer available");

            if (!product.Vendor.IsApproved || !product.Vendor.IsActive)
                throw new BadRequestException("This product's vendor is currently unavailable");

            ProductVariant? variant = null;

            if (dto.VariantId.HasValue)
            {
                variant = await _context.ProductVariants
                    .FirstOrDefaultAsync(v =>
                        v.Id == dto.VariantId.Value &&
                        v.ProductId == dto.ProductId &&
                        v.IsActive);

                if (variant == null)
                    throw new BadRequestException(
                        "The selected variant is not available for this product.");
            }
            else
            {
                var hasVariants = await _context.ProductVariants
                    .AnyAsync(v => v.ProductId == dto.ProductId && v.IsActive);

                if (hasVariants)
                    throw new BadRequestException(
                        "Please select a variant (e.g. size or color) before adding to cart.");
            }

            var availableStock = variant?.StockQuantity ?? product.StockQuantity;

            if (availableStock < dto.Quantity)
                throw new BadRequestException($"Only {availableStock} items available in stock");

            var existingItem = await _context.CartItems
                .Include(ci => ci.Product)
                    .ThenInclude(p => p.Images)
                .Include(ci => ci.Product.Vendor)
                .Include(ci => ci.Variant)
                .FirstOrDefaultAsync(ci =>
                    ci.UserId == userId &&
                    ci.ProductId == dto.ProductId &&
                    ci.VariantId == dto.VariantId);

            if (existingItem != null)
            {
                var newQuantity = existingItem.Quantity + dto.Quantity;

                if (availableStock < newQuantity)
                    throw new BadRequestException(
                        $"Cannot add more. Only {availableStock} items available");

                existingItem.Quantity = newQuantity;
                await _context.SaveChangesAsync();
                return MapToDto(existingItem);
            }

            var cartItem = new CartItem
            {
                UserId = userId,
                ProductId = dto.ProductId,
                VariantId = dto.VariantId,
                Quantity = dto.Quantity,
                AddedAt = DateTime.UtcNow
            };

            _context.CartItems.Add(cartItem);
            await _context.SaveChangesAsync();

            cartItem = await _context.CartItems
                .Include(ci => ci.Product)
                    .ThenInclude(p => p.Images)
                .Include(ci => ci.Product.Vendor)
                .Include(ci => ci.Variant)
                .FirstAsync(ci => ci.Id == cartItem.Id);

            return MapToDto(cartItem);
        }

        public async Task<CartItemDto> UpdateCartItemAsync(
            string userId, int cartItemId, UpdateCartItemDto dto)
        {
            var cartItem = await _context.CartItems
                .Include(ci => ci.Product)
                    .ThenInclude(p => p.Images)
                .Include(ci => ci.Product.Vendor)
                .Include(ci => ci.Variant)
                .FirstOrDefaultAsync(ci => ci.Id == cartItemId && ci.UserId == userId);

            if (cartItem == null)
                throw new NotFoundException("Cart item not found");

            var availableStock = cartItem.Variant?.StockQuantity
                                 ?? cartItem.Product.StockQuantity;

            if (availableStock < dto.Quantity)
                throw new BadRequestException(
                    $"Only {availableStock} items available in stock");

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


        private static CartItemDto MapToDto(CartItem cartItem)
        {
            var basePrice = cartItem.Product.DiscountPrice ?? cartItem.Product.Price;

            var priceAdjustment = cartItem.Variant?.PriceAdjustment ?? 0m;
            var unitPrice = basePrice + priceAdjustment;

            var primaryImage = cartItem.Product.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
                               ?? cartItem.Product.Images.FirstOrDefault()?.ImageUrl;

            var stockAvailable = cartItem.Variant?.StockQuantity
                                 ?? cartItem.Product.StockQuantity;

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
                StockAvailable = stockAvailable,
                InStock = stockAvailable >= cartItem.Quantity,
                VendorId = cartItem.Product.VendorId,
                VendorName = cartItem.Product.Vendor.StoreName,
                AddedAt = cartItem.AddedAt,

                VariantId = cartItem.VariantId,
                VariantTypeName = cartItem.Variant?.TypeName,
                VariantValue = cartItem.Variant?.Value,
                VariantColorHex = cartItem.Variant?.ColorHex,
                VariantPriceAdjustment = priceAdjustment
            };
        }
    }
}
