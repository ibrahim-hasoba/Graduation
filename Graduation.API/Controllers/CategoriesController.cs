using Graduation.DAL.Data;
using Shared.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs.Product;
// FIX #13: Use the shared CategoryDto — do NOT redefine it locally. The local duplicate
// class has been removed entirely. Shared.DTOs.Category.CategoryDto is used throughout.
using Shared.DTOs.Category;
using Graduation.DAL.Entities;

namespace Graduation.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoriesController : ControllerBase
    {
        private readonly DatabaseContext _context;

        public CategoriesController(DatabaseContext context)
        {
            _context = context;
        }

        /// <summary>Get all categories (public)</summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllCategories()
        {
            var categories = await _context.Categories
                .Where(c => c.IsActive && c.ParentCategoryId == null)
                .Include(c => c.SubCategories.Where(s => s.IsActive))
                .Select(c => new CategoryDto
                {
                    Id = c.Id,
                    NameAr = c.NameAr,
                    NameEn = c.NameEn,
                    Description = c.Description,
                    ImageUrl = c.ImageUrl,
                    ProductCount = c.Products.Count(p => p.IsActive)
                })
                .ToListAsync();

            return Ok(new ApiResult(data: categories));
        }

        /// <summary>Get category by ID with products (public)</summary>
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetCategoryById(int id)
        {
            var category = await _context.Categories
                .Include(c => c.SubCategories.Where(s => s.IsActive))
                .FirstOrDefaultAsync(c => c.Id == id && c.IsActive);

            if (category == null)
                return NotFound(new ApiResponse(404, "Category not found"));

            // FIX #7: Collect all sub-category IDs first, then run a single async COUNT
            // query instead of calling _context.Products.Count() synchronously for each
            // sub-category inside the projection loop.
            var subCategoryIds = category.SubCategories.Select(s => s.Id).ToList();

            var subProductCounts = await _context.Products
                .Where(p => subCategoryIds.Contains(p.CategoryId) && p.IsActive)
                .GroupBy(p => p.CategoryId)
                .Select(g => new { CategoryId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.CategoryId, x => x.Count);

            var categoryDto = new CategoryDto
            {
                Id = category.Id,
                NameAr = category.NameAr,
                NameEn = category.NameEn,
                Description = category.Description,
                ImageUrl = category.ImageUrl,
                ProductCount = await _context.Products.CountAsync(p => p.CategoryId == id && p.IsActive)
            };

            return Ok(new ApiResult(data: categoryDto));
        }

        /// <summary>Get products by category (public)</summary>
        [HttpGet("{id}/products")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCategoryProducts(
            int id,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var products = await _context.Products
                .Include(p => p.Vendor)
                .Include(p => p.Category)
                .Include(p => p.Images)
                .Include(p => p.Reviews.Where(r => r.IsApproved))
                .Where(p => p.CategoryId == id && p.IsActive)
                .OrderByDescending(p => p.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var totalCount = await _context.Products
                .CountAsync(p => p.CategoryId == id && p.IsActive);

            var productDtos = products.Select(p => new ProductListDto
            {
                Id = p.Id,
                NameAr = p.NameAr,
                NameEn = p.NameEn,
                Price = p.Price,
                DiscountPrice = p.DiscountPrice,
                FinalPrice = p.DiscountPrice ?? p.Price,
                DiscountPercentage = p.DiscountPrice.HasValue
                    ? (int)Math.Round(((p.Price - p.DiscountPrice.Value) / p.Price) * 100)
                    : 0,
                InStock = p.StockQuantity > 0,
                IsFeatured = p.IsFeatured,
                PrimaryImageUrl = p.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
                    ?? p.Images.FirstOrDefault()?.ImageUrl,
                AverageRating = p.Reviews.Any() ? Math.Round(p.Reviews.Average(r => r.Rating), 1) : 0,
                TotalReviews = p.Reviews.Count,
                VendorName = p.Vendor.StoreName,
                CategoryNameEn = p.Category.NameEn,
                CategoryNameAr = p.Category.NameAr
            }).ToList();

            return Ok(new ApiResult(data: new
            {
                products = productDtos,
                totalCount,
                pageNumber,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            }));
        }

        /// <summary>
        /// Get all leaf categories (only subcategories where products can be added).
        /// Returns hierarchy path like "Electronics → Mobile → iPhone"
        /// </summary>
        [HttpGet("leaf-categories")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetLeafCategories()
        {
            var allCategories = await _context.Categories
                .Where(c => c.IsActive)
                .AsNoTracking()
                .ToListAsync();

            var categoryMap = allCategories.ToDictionary(c => c.Id);

            var leafCategories = allCategories
                .Where(c => !allCategories.Any(other => other.ParentCategoryId == c.Id))
                .ToList();

            var leafDtos = leafCategories.Select(c =>
            {
                var pathEn = BuildCategoryPathStringInMemory(c, categoryMap);
                return new
                {
                    id = c.Id,
                    nameEn = c.NameEn,
                    nameAr = c.NameAr,
                    description = c.Description,
                    pathEn,
                    canAddProducts = true
                };
            }).OrderBy(x => x.pathEn).ToList();

            return Ok(new ApiResult(data: leafDtos, message: "All leaf categories where products can be added", count: leafDtos.Count));
        }

        private string BuildCategoryPathStringInMemory(
            Category category,
            Dictionary<int, Category> categoryMap)
        {
            var path = new List<string> { category.NameEn };
            var visited = new HashSet<int>();
            var current = category;

            while (current.ParentCategoryId.HasValue && !visited.Contains(current.Id))
            {
                visited.Add(current.Id);
                if (!categoryMap.TryGetValue(current.ParentCategoryId.Value, out var parent))
                    break;
                path.Insert(0, parent.NameEn);
                current = parent;
            }

            return string.Join(" → ", path);
        }
    }
}
