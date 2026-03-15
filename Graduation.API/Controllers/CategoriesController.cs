using Graduation.BLL.Services.Implementations;
using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs.Category;
using Shared.DTOs.Product;
using Shared.Errors;

namespace Graduation.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoriesController : ControllerBase
    {
        private readonly DatabaseContext _context;
        private readonly ICodeLookupService _codeLookup;

        public CategoriesController(DatabaseContext context, ICodeLookupService codeLookup)
        {
            _context = context;
            _codeLookup = codeLookup;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllCategories(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var query = _context.Categories
                .Where(c => c.Status == CategoryStatus.Active && c.ParentCategoryId == null)
                .Include(c => c.SubCategories.Where(s => s.Status == CategoryStatus.Active))
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                query = query.Where(c =>
                    c.NameEn.ToLower().Contains(s) || c.NameAr.Contains(search));
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var idList = await query.Select(c => c.Id).ToListAsync();
            var subIdList = await _context.Categories
                .Where(c => idList.Contains(c.ParentCategoryId ?? 0) && c.Status == CategoryStatus.Active)
                .Select(c => c.Id).ToListAsync();

            var productCounts = await _context.Products
                .Where(p => idList.Union(subIdList).Contains(p.CategoryId) && p.IsActive)
                .GroupBy(p => p.CategoryId)
                .Select(g => new { CategoryId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.CategoryId, x => x.Count);

            var categories = await query
                .OrderBy(c => c.NameEn)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var dtos = categories.Select(c => new CategoryDto
            {
                Code = c.Code,
                Id = c.Id,
                NameAr = c.NameAr,
                NameEn = c.NameEn,
                Description = c.Description,
                ImageUrl = c.ImageUrl,
                ProductCount = productCounts.GetValueOrDefault(c.Id, 0),
                Status = c.Status.ToString(),
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt
            }).ToList();

            return Ok(new ApiResult(data: new
            {
                categories,
                totalCount,
                pageNumber,
                pageSize,
                totalPages,
                hasPreviousPage = pageNumber > 1,
                hasNextPage = pageNumber < totalPages
            }, count: totalCount));
        }

        [HttpGet("{categoryCode}")]
        public async Task<IActionResult> GetCategoryById(string categoryCode)
        {
            var id = await _codeLookup.ResolveCategoryIdAsync(categoryCode);

            var category = await _context.Categories
                .Include(c => c.SubCategories.Where(s => s.Status == CategoryStatus.Active))
                .FirstOrDefaultAsync(c => c.Id == id && c.Status == CategoryStatus.Active);

            if (category == null)
                return NotFound(new ApiResponse(404, "Category not found"));

            var productCount = await _context.Products
                .CountAsync(p => p.CategoryId == id && p.IsActive);

            var dto = new CategoryDto
            {
                Code = category.Code,
                Id = category.Id,
                NameAr = category.NameAr,
                NameEn = category.NameEn,
                Description = category.Description,
                ImageUrl = category.ImageUrl,
                ProductCount = productCount,
                Status = category.Status.ToString(),
                CreatedAt = category.CreatedAt,
                UpdatedAt = category.UpdatedAt,
                SubCategories = category.SubCategories.Select(s => new CategoryDto
                {
                    Code = s.Code,
                    Id = s.Id,
                    NameAr = s.NameAr,
                    NameEn = s.NameEn,
                    ImageUrl = s.ImageUrl,
                    Status = s.Status.ToString(),
                    CreatedAt = s.CreatedAt,
                    UpdatedAt = s.UpdatedAt
                }).ToList()
            };

            return Ok(new ApiResult(data: dto));
        }

        [HttpGet("{categoryCode}/products")]
        public async Task<IActionResult> GetCategoryProducts(
            string categoryCode,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var id = await _codeLookup.ResolveCategoryIdAsync(categoryCode);

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
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var productDtos = products.Select(p => new ProductListDto
            {
                Id = p.Id,
                Code = p.Code,
                NameAr = p.NameAr,
                NameEn = p.NameEn,
                Price = p.Price,
                DiscountPrice = p.DiscountPrice,
                FinalPrice = p.DiscountPrice ?? p.Price,
                DiscountPercentage = p.DiscountPrice.HasValue
                    ? (int)Math.Round(((p.Price - p.DiscountPrice.Value) / p.Price) * 100) : 0,
                InStock = p.StockQuantity > 0,
                IsFeatured = p.IsFeatured,
                PrimaryImageUrl = p.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
                    ?? p.Images.FirstOrDefault()?.ImageUrl,
                AverageRating = p.Reviews.Any()
                    ? Math.Round(p.Reviews.Average(r => r.Rating), 1) : 0,
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
                totalPages,
                hasPreviousPage = pageNumber > 1,
                hasNextPage = pageNumber < totalPages
            }));
        }

        [HttpGet("leaf-categories")]
        public async Task<IActionResult> GetLeafCategories()
        {
            var allCategories = await _context.Categories
                .Where(c => c.Status == CategoryStatus.Active)
                .AsNoTracking()
                .ToListAsync();

            var categoryMap = allCategories.ToDictionary(c => c.Id);
            var leafCategories = allCategories
                .Where(c => !allCategories.Any(other => other.ParentCategoryId == c.Id))
                .ToList();

            var leafDtos = leafCategories.Select(c =>
            {
                var pathEn = BuildPath(c, categoryMap);
                return new
                {
                    categoryCode = c.Code,
                    nameEn = c.NameEn,
                    nameAr = c.NameAr,
                    description = c.Description,
                    status = c.Status.ToString(),
                    createdAt = c.CreatedAt,
                    updatedAt = c.UpdatedAt,
                    pathEn,
                    canAddProducts = true
                };
            }).OrderBy(x => x.pathEn).ToList();

            return Ok(new ApiResult(
                data: leafDtos,
                message: "All leaf categories where products can be added",
                count: leafDtos.Count));
        }

        private static string BuildPath(Category c, Dictionary<int, Category> map)
        {
            var path = new List<string> { c.NameEn };
            var visited = new HashSet<int>();
            var current = c;
            while (current.ParentCategoryId.HasValue && !visited.Contains(current.Id))
            {
                visited.Add(current.Id);
                if (!map.TryGetValue(current.ParentCategoryId.Value, out var parent)) break;
                path.Insert(0, parent.NameEn);
                current = parent;
            }
            return string.Join(" → ", path);
        }
    }
}
