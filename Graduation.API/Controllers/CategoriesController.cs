using Graduation.DAL.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs.Product;
using Shared.DTOs.Category;

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

        /// <summary>
        /// Get all categories (public)
        /// </summary>
        [HttpGet]
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
                    ProductCount = c.Products.Count(p => p.IsActive),
                    SubCategories = c.SubCategories.Select(s => new CategoryDto
                    {
                        Id = s.Id,
                        NameAr = s.NameAr,
                        NameEn = s.NameEn,
                        Description = s.Description,
                        ImageUrl = s.ImageUrl,
                        ProductCount = s.Products.Count(p => p.IsActive)
                    }).ToList()
                })
                .ToListAsync();

            return Ok(new { success = true, data = categories });
        }

        /// <summary>
        /// Get category by ID with products (public)
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetCategoryById(int id)
        {
            var category = await _context.Categories
                .Include(c => c.SubCategories.Where(s => s.IsActive))
                .FirstOrDefaultAsync(c => c.Id == id && c.IsActive);

            if (category == null)
                return NotFound(new { success = false, message = "Category not found" });

            var categoryDto = new CategoryDto
            {
                Id = category.Id,
                NameAr = category.NameAr,
                NameEn = category.NameEn,
                Description = category.Description,
                ImageUrl = category.ImageUrl,
                ProductCount = await _context.Products.CountAsync(p => p.CategoryId == id && p.IsActive),
                SubCategories = category.SubCategories.Select(s => new CategoryDto
                {
                    Id = s.Id,
                    NameAr = s.NameAr,
                    NameEn = s.NameEn,
                    Description = s.Description,
                    ImageUrl = s.ImageUrl,
                    ProductCount = _context.Products.Count(p => p.CategoryId == s.Id && p.IsActive)
                }).ToList()
            };

            return Ok(new { success = true, data = categoryDto });
        }

        /// <summary>
        /// Get products by category (public)
        /// </summary>
        [HttpGet("{id}/products")]
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

            return Ok(new
            {
                success = true,
                data = new
                {
                    products = productDtos,
                    totalCount,
                    pageNumber,
                    pageSize,
                    totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                }
            });
        }

        /// <summary>
        /// Get all leaf categories (only subcategories where products can be added)
        /// Returns hierarchy path like "Electronics → Mobile → iPhone"
        /// </summary>
        [HttpGet("leaf-categories")]
        public async Task<IActionResult> GetLeafCategories()
        {
            var leafCategories = await _context.Categories
                .Where(c => c.IsActive && !c.SubCategories.Any())
                .AsNoTracking()
                .ToListAsync();

            var leafDtos = leafCategories.Select(c =>
            {
                var pathEn = BuildCategoryPathString(c);
                return new
                {
                    id = c.Id,
                    nameEn = c.NameEn,
                    nameAr = c.NameAr,
                    description = c.Description,
                    pathEn = pathEn,
                    canAddProducts = true
                };
            }).OrderBy(x => x.pathEn).ToList();

            return Ok(new
            {
                success = true,
                message = "All leaf categories where products can be added",
                totalCount = leafDtos.Count,
                data = leafDtos
            });
        }

        private string BuildCategoryPathString(Graduation.DAL.Entities.Category category)
        {
            var path = new List<string> { category.NameEn };
            var current = category;
            var visited = new HashSet<int>();

            while (current.ParentCategoryId != null && !visited.Contains(current.Id))
            {
                visited.Add(current.Id);
                var parent = _context.Categories.FirstOrDefault(c => c.Id == current.ParentCategoryId);
                if (parent == null) break;
                path.Insert(0, parent.NameEn);
                current = parent;
            }

            return string.Join(" → ", path);
        }
    }

    public class CategoryDto
    {
        public int Id { get; set; }
        public string NameAr { get; set; } = string.Empty;
        public string NameEn { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public int ProductCount { get; set; }
        public List<CategoryDto> SubCategories { get; set; } = new();
    }
}
