using Shared.Errors;
using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.DTOs.Wishlist;

namespace Graduation.BLL.Services.Implementations
{
  public class WishlistService : IWishlistService
  {
    private readonly DatabaseContext _context;
    private readonly ILogger<WishlistService> _logger;

    public WishlistService(
        DatabaseContext context,
        ILogger<WishlistService> logger)
    {
      _context = context;
      _logger = logger;
    }

    public async Task<WishlistDto> AddToWishlistAsync(string userId, int productId)
    {
      var product = await _context.Products
          .Include(p => p.Vendor)
          .Include(p => p.Images)
          .Include(p => p.Reviews)
          .FirstOrDefaultAsync(p => p.Id == productId && p.IsActive);

      if (product == null)
        throw new NotFoundException("Product not found");

      var existingWishlist = await _context.Wishlists
          .FirstOrDefaultAsync(w => w.UserId == userId && w.ProductId == productId);

      if (existingWishlist != null)
        throw new ConflictException("Product already in wishlist");

      var wishlist = new Wishlist
      {
        UserId = userId,
        ProductId = productId,
        CreatedAt = DateTime.UtcNow
      };

      _context.Wishlists.Add(wishlist);
      await _context.SaveChangesAsync();

      _logger.LogInformation("Product added to wishlist: ProductId={ProductId}, UserId={UserId}",
          productId, userId);

      return MapToWishlistDto(wishlist, product);
    }

    public async Task RemoveFromWishlistAsync(string userId, int productId)
    {
      var wishlist = await _context.Wishlists
          .FirstOrDefaultAsync(w => w.UserId == userId && w.ProductId == productId);

      if (wishlist == null)
        throw new NotFoundException("Wishlist item not found");

      _context.Wishlists.Remove(wishlist);
      await _context.SaveChangesAsync();

      _logger.LogInformation("Product removed from wishlist: ProductId={ProductId}, UserId={UserId}",
          productId, userId);
    }

    public async Task<List<WishlistDto>> GetUserWishlistAsync(string userId)
    {
      var wishlistItems = await _context.Wishlists
          .Where(w => w.UserId == userId)
          .Include(w => w.Product)
              .ThenInclude(p => p!.Vendor)
          .Include(w => w.Product)
              .ThenInclude(p => p!.Images)
          .Include(w => w.Product)
              .ThenInclude(p => p!.Reviews)
          .OrderByDescending(w => w.CreatedAt)
          .ToListAsync();

      var dtos = wishlistItems
          .Select(w => MapToWishlistDto(w, w.Product!))
          .ToList();

      return dtos;
    }

    public async Task<bool> IsInWishlistAsync(string userId, int productId)
    {
      return await _context.Wishlists
          .AnyAsync(w => w.UserId == userId && w.ProductId == productId);
    }

    public async Task ClearWishlistAsync(string userId)
    {
      var wishlistItems = await _context.Wishlists
          .Where(w => w.UserId == userId)
          .ToListAsync();

      _context.Wishlists.RemoveRange(wishlistItems);
      await _context.SaveChangesAsync();

      _logger.LogInformation("Wishlist cleared: UserId={UserId}", userId);
    }

    private WishlistDto MapToWishlistDto(Wishlist wishlist, Product product)
    {
      return new WishlistDto
      {
        Id = wishlist.Id,
        ProductId = product.Id,
        ProductName = product.NameEn,
        ProductNameAr = product.NameAr,
        Price = product.Price,
        DiscountPrice = product.DiscountPrice,
        FinalPrice = product.DiscountPrice ?? product.Price,
        ImageUrl = product.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
              ?? product.Images.FirstOrDefault()?.ImageUrl,
        VendorName = product.Vendor?.StoreName ?? "Unknown",
        VendorId = product.VendorId,
        InStock = product.StockQuantity > 0,
        AverageRating = product.Reviews.Any() ? Math.Round(product.Reviews.Average(r => r.Rating), 1) : 0,
        TotalReviews = product.Reviews.Count,
        AddedAt = wishlist.CreatedAt
      };
    }
  }
}
