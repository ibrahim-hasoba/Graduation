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
                .AsNoTracking()
                .Include(ci => ci.Product)
                    .ThenInclude(p => p.Images)
                .Include(ci => ci.Product.Vendor)
                .Include(ci => ci.SelectedVariants)
                    .ThenInclude(sv => sv.ProductVariant)
                .AsSplitQuery() 
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
                    .Select(p => new { p.Id, p.IsActive, p.StockQuantity })
                    .FirstOrDefaultAsync(p => p.Id == dto.ProductId);

            if (product == null)
                throw new NotFoundException("Product", dto.ProductId);

            if (!product.IsActive)
                throw new BadRequestException("This product is no longer available");

            // 1. Fetch the requested variants
            List<ProductVariant> selectedVariants = new();
            var requestedVariantIds = dto.VariantIds ?? new List<int>();

            if (requestedVariantIds.Any())
            {
                selectedVariants = await _context.ProductVariants
                    .Where(v => requestedVariantIds.Contains(v.Id) && v.IsActive)
                    .ToListAsync();

                // Ensure all requested variants exist and belong to this product
                if (selectedVariants.Count != requestedVariantIds.Count ||
                    selectedVariants.Any(v => v.ProductId != dto.ProductId))
                {
                    throw new BadRequestException("One or more selected variants are invalid or not available.");
                }
            }
            else
            {
                // Verify if the product HAS variants and the user forgot to send them
                var hasVariants = await _context.ProductVariants
                    .AnyAsync(v => v.ProductId == dto.ProductId && v.IsActive);

                if (hasVariants)
                    throw new BadRequestException("Please select product variants (e.g. size or color) before adding to cart.");
            }

            // 2. Determine available stock using the lowest stock among selected variants
            var availableStock = selectedVariants.Any()
                ? selectedVariants.Min(v => v.StockQuantity)
                : product.StockQuantity;

            if (availableStock < dto.Quantity)
                throw new BadRequestException($"Only {availableStock} items available in stock");

            // 3. Check if this exact item with these EXACT variants already exists in the cart
            var existingItems = await _context.CartItems
                .Include(ci => ci.SelectedVariants)
                .Where(ci => ci.UserId == userId && ci.ProductId == dto.ProductId)
                .ToListAsync();

            var existingItem = existingItems.FirstOrDefault(ci =>
                ci.SelectedVariants.Count == requestedVariantIds.Count &&
                ci.SelectedVariants.All(sv => requestedVariantIds.Contains(sv.ProductVariantId)));

            if (existingItem != null)
            {
                var newQuantity = existingItem.Quantity + dto.Quantity;

                if (availableStock < newQuantity)
                    throw new BadRequestException($"Cannot add more. Only {availableStock} items available");

                existingItem.Quantity = newQuantity;
                await _context.SaveChangesAsync();

                return await GetCartItemDtoAsync(existingItem.Id);
            }

            // 4. Create new CartItem with multiple variants
            var cartItem = new CartItem
            {
                UserId = userId,
                ProductId = dto.ProductId,
                Quantity = dto.Quantity,
                AddedAt = DateTime.UtcNow,
                SelectedVariants = selectedVariants.Select(v => new CartItemVariant
                {
                    ProductVariantId = v.Id
                }).ToList()
            };

            _context.CartItems.Add(cartItem);
            await _context.SaveChangesAsync();

            return await GetCartItemDtoAsync(cartItem.Id);
        }

        public async Task<CartItemDto> UpdateCartItemAsync(
            string userId, int cartItemId, UpdateCartItemDto dto)
        {
            var cartItem = await _context.CartItems
                .Include(ci => ci.Product)
                    .ThenInclude(p => p.Images)
                .Include(ci => ci.Product.Vendor)
                .Include(ci => ci.SelectedVariants)
                    .ThenInclude(sv => sv.ProductVariant)
                .AsSplitQuery() 
                .FirstOrDefaultAsync(ci => ci.Id == cartItemId && ci.UserId == userId);

            if (cartItem == null)
                throw new NotFoundException("Cart item not found");

            var availableStock = cartItem.SelectedVariants.Any()
                ? cartItem.SelectedVariants.Min(sv => sv.ProductVariant.StockQuantity)
                : cartItem.Product.StockQuantity;

            if (availableStock < dto.Quantity)
                throw new BadRequestException(
                    $"Only {availableStock} items available in stock");

            cartItem.Quantity = dto.Quantity;
            await _context.SaveChangesAsync();

            return MapToDto(cartItem);
        }

        public async Task RemoveFromCartAsync(string userId, int cartItemId)
        {
            var rowsDeleted = await _context.CartItems
                .Where(ci => ci.Id == cartItemId && ci.UserId == userId)
                .ExecuteDeleteAsync();

            if (rowsDeleted == 0)
                throw new NotFoundException("Cart item not found");
        }

        public async Task ClearCartAsync(string userId)
        {
            await _context.CartItems
                .Where(ci => ci.UserId == userId)
                .ExecuteDeleteAsync();
        }

        public async Task<int> GetCartItemsCountAsync(string userId)
        {
            return await _context.CartItems
                .Where(ci => ci.UserId == userId)
                .SumAsync(ci => ci.Quantity);
        }

        private async Task<CartItemDto> GetCartItemDtoAsync(int cartItemId)
        {
            var cartItem = await _context.CartItems
                .AsNoTracking() 
                .Include(ci => ci.Product)
                    .ThenInclude(p => p.Images)
                .Include(ci => ci.Product.Vendor)
                .Include(ci => ci.SelectedVariants)
                    .ThenInclude(sv => sv.ProductVariant)
                .AsSplitQuery() 
                .FirstAsync(ci => ci.Id == cartItemId);

            return MapToDto(cartItem);
        }

        private static CartItemDto MapToDto(CartItem cartItem)
        {
            var basePrice = cartItem.Product.DiscountPrice ?? cartItem.Product.Price;

            // CHANGED: Sum up the price adjustments for all selected variants
            var priceAdjustment = cartItem.SelectedVariants.Sum(sv => sv.ProductVariant.PriceAdjustment);
            var unitPrice = basePrice + priceAdjustment;

            var primaryImage = cartItem.Product.Images?.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
                   ?? cartItem.Product.Images?.FirstOrDefault()?.ImageUrl;

            // CHANGED: Use the lowest stock among selected variants
            int stockAvailable;

            if (cartItem.SelectedVariants.Any())
            {
                stockAvailable = cartItem.SelectedVariants
                    .Min(sv => sv.ProductVariant.StockQuantity) ?? 0;
            }
            else
            {
                stockAvailable = cartItem.Product.StockQuantity;
            }

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

                SelectedVariants = cartItem.SelectedVariants?.Select(sv => new CartItemVariantDto
                {
                    VariantId = sv.ProductVariantId,
                    TypeName = sv.ProductVariant.TypeName,
                    Value = sv.ProductVariant.Value,
                    ColorHex = sv.ProductVariant.ColorHex,
                    PriceAdjustment = sv.ProductVariant.PriceAdjustment
                }).ToList() ?? new List<CartItemVariantDto>(),
                VariantPriceAdjustment = priceAdjustment
            };
        }
    }
}