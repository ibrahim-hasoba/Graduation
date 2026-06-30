using AutoMapper;
using Graduation.BLL.Errors;
using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Entities;
using Graduation.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Graduation.BLL.DTOs.Cart;

namespace Graduation.BLL.Services.Implementations
{
    public class CartService : ICartService
    {
        private readonly IUnitOfWork _uow;
        private readonly IMapper _mapper;

        public CartService(IUnitOfWork uow, IMapper mapper)
        {
            _uow = uow;
            _mapper = mapper;
        }

        public async Task<CartDto> GetUserCartAsync(string userId)
        {
            var cartItems = await _uow.Repository<CartItem>().Query()
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

            var product = await _uow.Repository<Product>().Query()
                    .Select(p => new { p.Id, p.IsActive, p.StockQuantity })
                    .FirstOrDefaultAsync(p => p.Id == dto.ProductId);

            if (product == null)
                throw new NotFoundException("Product", dto.ProductId);

            if (!product.IsActive)
                throw new BadRequestException("This product is no longer available");

            List<ProductVariant> selectedVariants = new();
            var requestedVariantIds = dto.VariantIds ?? new List<int>();

            if (requestedVariantIds.Any())
            {
                selectedVariants = await _uow.Repository<ProductVariant>().Query()
                    .Where(v => requestedVariantIds.Contains(v.Id) && v.IsActive)
                    .ToListAsync();

                if (selectedVariants.Count != requestedVariantIds.Count ||
                    selectedVariants.Any(v => v.ProductId != dto.ProductId))
                {
                    throw new BadRequestException("One or more selected variants are invalid or not available.");
                }
            }
            else
            {
                var hasVariants = await _uow.Repository<ProductVariant>().Query()
                    .AnyAsync(v => v.ProductId == dto.ProductId && v.IsActive);

                if (hasVariants)
                    throw new BadRequestException("Please select product variants (e.g. size or color) before adding to cart.");
            }

            var availableStock = selectedVariants.Any()
                ? selectedVariants.Min(v => v.StockQuantity)
                : product.StockQuantity;

            if (availableStock < dto.Quantity)
                throw new BadRequestException($"Only {availableStock} items available in stock");

            var existingItems = await _uow.Repository<CartItem>().Query()
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
                await _uow.SaveChangesAsync();

                return await GetCartItemDtoAsync(existingItem.Id);
            }

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

            _uow.Repository<CartItem>().Add(cartItem);
            await _uow.SaveChangesAsync();

            return await GetCartItemDtoAsync(cartItem.Id);
        }

        public async Task<CartItemDto> UpdateCartItemAsync(string userId, int cartItemId, UpdateCartItemDto dto)
        {
            var cartItem = await _uow.Repository<CartItem>().Query()
                .Include(ci => ci.Product)
                    .ThenInclude(p => p.Images)
                .Include(ci => ci.Product.Vendor)
                .Include(ci => ci.SelectedVariants)
                    .ThenInclude(sv => sv.ProductVariant)
                .AsSplitQuery()
                .FirstOrDefaultAsync(ci => ci.Id == cartItemId && ci.UserId == userId);

            if (cartItem == null)
                throw new NotFoundException("Cart item not found");

            var variantsToCheck = cartItem.SelectedVariants.Select(sv => sv.ProductVariant).ToList();

            if (dto.VariantIds != null)
            {
                var requestedVariantIds = dto.VariantIds;

                if (requestedVariantIds.Any())
                {
                    var newVariants = await _uow.Repository<ProductVariant>().Query()
                        .Where(v => requestedVariantIds.Contains(v.Id) && v.IsActive)
                        .ToListAsync();

                    if (newVariants.Count != requestedVariantIds.Count ||
                        newVariants.Any(v => v.ProductId != cartItem.ProductId))
                    {
                        throw new BadRequestException("One or more selected variants are invalid or not available.");
                    }

                    var existingDuplicate = await _uow.Repository<CartItem>().Query()
                        .Include(ci => ci.SelectedVariants)
                        .Where(ci => ci.UserId == userId && ci.ProductId == cartItem.ProductId && ci.Id != cartItemId)
                        .FirstOrDefaultAsync(ci =>
                            ci.SelectedVariants.Count == requestedVariantIds.Count &&
                            ci.SelectedVariants.All(sv => requestedVariantIds.Contains(sv.ProductVariantId)));

                    if (existingDuplicate != null)
                        throw new BadRequestException("An item with these exact variants already exists in your cart. Please update its quantity instead.");

                    _uow.Repository<CartItemVariant>().DeleteRange(cartItem.SelectedVariants);

                    cartItem.SelectedVariants = newVariants.Select(v => new CartItemVariant
                    {
                        ProductVariantId = v.Id
                    }).ToList();

                    variantsToCheck = newVariants;
                }
                else
                {
                    var hasVariants = await _uow.Repository<ProductVariant>().Query()
                        .AnyAsync(v => v.ProductId == cartItem.ProductId && v.IsActive);

                    if (hasVariants)
                        throw new BadRequestException("Please select product variants (e.g. size or color).");

                    _uow.Repository<CartItemVariant>().DeleteRange(cartItem.SelectedVariants);
                    cartItem.SelectedVariants.Clear();
                    variantsToCheck.Clear();
                }
            }

            var availableStock = variantsToCheck.Any()
                ? variantsToCheck.Min(v => v.StockQuantity)
                : cartItem.Product.StockQuantity;

            if (availableStock < dto.Quantity)
                throw new BadRequestException($"Only {availableStock} items available in stock");

            cartItem.Quantity = dto.Quantity;
            await _uow.SaveChangesAsync();

            return await GetCartItemDtoAsync(cartItem.Id);
        }

        public async Task RemoveFromCartAsync(string userId, int cartItemId)
        {
            var repo = _uow.Repository<CartItem>();
            var item = await repo.Query()
                .FirstOrDefaultAsync(ci => ci.Id == cartItemId && ci.UserId == userId);

            if (item == null)
                throw new NotFoundException("Cart item not found");

            repo.Delete(item);
            await _uow.SaveChangesAsync();
        }

        public async Task ClearCartAsync(string userId)
        {
            var items = await _uow.Repository<CartItem>().Query()
                .Where(ci => ci.UserId == userId)
                .ToListAsync();

            _uow.Repository<CartItem>().DeleteRange(items);
            await _uow.SaveChangesAsync();
        }

        public async Task<int> GetCartItemsCountAsync(string userId)
        {
            return await _uow.Repository<CartItem>().Query()
                .Where(ci => ci.UserId == userId)
                .SumAsync(ci => ci.Quantity);
        }

        private async Task<CartItemDto> GetCartItemDtoAsync(int cartItemId)
        {
            var cartItem = await _uow.Repository<CartItem>().Query()
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

            var priceAdjustment = cartItem.SelectedVariants.Sum(sv => sv.ProductVariant.PriceAdjustment);
            var unitPrice = basePrice + priceAdjustment;

            var primaryImage = cartItem.Product.Images?.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
                   ?? cartItem.Product.Images?.FirstOrDefault()?.ImageUrl;

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
