using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.DTOs.Category;
using Shared.Errors;

namespace Graduation.BLL.Services.Implementations
{
    public class CategoryService : ICategoryService
    {
        private readonly DatabaseContext _context;
        private readonly ICodeAssignmentService _codeAssignment;
        private readonly ICodeLookupService _codeLookup;
        private readonly ILogger<CategoryService> _logger;

        public CategoryService(
            DatabaseContext context,
            ICodeAssignmentService codeAssignment,
            ICodeLookupService codeLookup,
            ILogger<CategoryService> logger)
        {
            _context = context;
            _codeAssignment = codeAssignment;
            _codeLookup = codeLookup;
            _logger = logger;
        }


        public async Task<CategoryDto> CreateCategoryAsync(CreateCategoryDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.NameEn) || string.IsNullOrWhiteSpace(dto.NameAr))
                throw new BadRequestException("Category names in both English and Arabic are required");

            if (dto.ParentCategoryId.HasValue && dto.ParentCategoryId.Value > 0)
            {
                var parentExists = await _context.Categories
                    .AnyAsync(c => c.Id == dto.ParentCategoryId
                               && c.Status == CategoryStatus.Active);
                if (!parentExists)
                    throw new NotFoundException("Parent category not found");
            }
            else
            {
                dto.ParentCategoryId = null;
            }

            var duplicateName = await _context.Categories
                .AnyAsync(c =>
                    (c.NameEn.ToLower() == dto.NameEn.ToLower() || c.NameAr == dto.NameAr)
                    && c.Status == CategoryStatus.Active);
            if (duplicateName)
                throw new ConflictException("A category with this name already exists");

            var category = new Category
            {
                NameEn = dto.NameEn.Trim(),
                NameAr = dto.NameAr.Trim(),
                Description = dto.Description?.Trim(),
                ImageUrl = dto.ImageUrl?.Trim(),
                ParentCategoryId = dto.ParentCategoryId,
                Status = CategoryStatus.Active,
                CreatedAt = DateTime.UtcNow
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            await _codeAssignment.AssignCategoryCodeAsync(category);

            _logger.LogInformation("Category created: {Name} (Code: {Code})",
                dto.NameEn, category.Code);

            return await GetCategoryByCodeAsync(category.Code!);
        }


        public async Task<CategoryDto> UpdateCategoryAsync(string categoryCode, UpdateCategoryDto dto)
        {
            var id = await _codeLookup.ResolveCategoryIdAsync(categoryCode);
            var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == id);
            if (category == null)
                throw new NotFoundException("Category not found");

            if (!string.IsNullOrWhiteSpace(dto.NameEn) &&
                !dto.NameEn.Equals(category.NameEn, StringComparison.OrdinalIgnoreCase))
            {
                var dup = await _context.Categories
                    .AnyAsync(c => c.Id != id
                               && c.NameEn.ToLower() == dto.NameEn.ToLower()
                               && c.Status == CategoryStatus.Active);
                if (dup) throw new ConflictException("Another active category has this English name");
                category.NameEn = dto.NameEn.Trim();
            }

            if (!string.IsNullOrWhiteSpace(dto.NameAr) && dto.NameAr != category.NameAr)
            {
                var dup = await _context.Categories
                    .AnyAsync(c => c.Id != id
                               && c.NameAr == dto.NameAr
                               && c.Status == CategoryStatus.Active);
                if (dup) throw new ConflictException("Another active category has this Arabic name");
                category.NameAr = dto.NameAr.Trim();
            }

            if (dto.Description != null) category.Description = dto.Description.Trim();
            if (dto.ImageUrl != null) category.ImageUrl = dto.ImageUrl.Trim();

            int? newParentId = dto.ParentCategoryId.HasValue && dto.ParentCategoryId.Value > 0
                ? dto.ParentCategoryId : null;

            if (newParentId != category.ParentCategoryId)
            {
                if (newParentId.HasValue)
                {
                    var parentExists = await _context.Categories
                        .AnyAsync(c => c.Id == newParentId && c.Status == CategoryStatus.Active);
                    if (!parentExists)
                        throw new NotFoundException("Parent category not found");

                    if (await HasCircularReferenceAsync(id, newParentId.Value))
                        throw new BusinessException("Cannot set this as parent — circular reference detected");
                }
                category.ParentCategoryId = newParentId;
            }

            category.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Category updated: {Name} (Code: {Code})",
                category.NameEn, category.Code);

            return await GetCategoryByCodeAsync(categoryCode);
        }


        public async Task<CategoryDto> ToggleActivationAsync(string categoryCode)
        {
            var id = await _codeLookup.ResolveCategoryIdAsync(categoryCode);
            var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == id);
            if (category == null)
                throw new NotFoundException("Category not found");

            if (category.Status == CategoryStatus.Active)
            {
                // Deactivate — cascade to subcategories
                category.Status = CategoryStatus.Inactive;
                await DeactivateSubcategoriesAsync(id);
            }
            else
            {
                // Re-activate — do NOT auto-activate subcategories;
                // admin can toggle each one individually
                category.Status = CategoryStatus.Active;
            }

            category.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var action = category.Status == CategoryStatus.Active ? "activated" : "deactivated";
            _logger.LogInformation("Category {Action}: {Name} (Code: {Code})",
                action, category.NameEn, category.Code);

            return await GetCategoryByCodeAsync(categoryCode);
        }


        public async Task DeleteCategoryAsync(string categoryCode)
        {
            var id = await _codeLookup.ResolveCategoryIdAsync(categoryCode);
            var category = await _context.Categories
                .Include(c => c.SubCategories)
                .Include(c => c.Products)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
                throw new NotFoundException("Category not found");

            if (category.Products.Any())
                throw new BadRequestException(
                    $"Cannot delete category '{category.NameEn}' — it has {category.Products.Count} " +
                    "product(s) assigned to it. Reassign or remove those products first.");

            if (category.SubCategories.Any())
                throw new BadRequestException(
                    $"Cannot delete category '{category.NameEn}' — it has " +
                    $"{category.SubCategories.Count} subcategory(ies). Delete them first.");

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Category hard-deleted: {Name} (Code: {Code})",
                category.NameEn, category.Code);
        }


        public async Task<PagedCategoryResultDto> GetAllCategoriesAsync(CategoryQueryDto query)
        {
            // Clamp pagination
            if (query.PageNumber < 1) query.PageNumber = 1;
            if (query.PageSize < 1 || query.PageSize > 100) query.PageSize = 20;

            // Build base query — root categories only (parent = null) unless a specific
            // parent is requested. Sub-categories are returned nested inside their parent.
            var dbQuery = _context.Categories
                .Include(c => c.SubCategories)
                .AsQueryable();

            // Status filter
            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                if (Enum.TryParse<CategoryStatus>(query.Status, ignoreCase: true, out var statusEnum))
                    dbQuery = dbQuery.Where(c => c.Status == statusEnum);
            }

            // Parent filter: 0 or not supplied → root only
            if (query.ParentId.HasValue)
                dbQuery = query.ParentId.Value == 0
                    ? dbQuery.Where(c => c.ParentCategoryId == null)
                    : dbQuery.Where(c => c.ParentCategoryId == query.ParentId.Value);
            else
                dbQuery = dbQuery.Where(c => c.ParentCategoryId == null);

            // Search filter (applied after parent filter to search within the level)
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var s = query.Search.ToLower();
                dbQuery = dbQuery.Where(c =>
                    c.NameEn.ToLower().Contains(s) ||
                    c.NameAr.Contains(query.Search));
            }

            var totalCount = await dbQuery.CountAsync();

            var categories = await dbQuery
                .OrderBy(c => c.NameEn)
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync();

            // Collect all IDs in the result (including subcategory IDs) for a single product count query
            var allIds = categories
                .SelectMany(c => GetAllIdsFromTree(c))
                .Distinct()
                .ToList();

            var productCounts = await _context.Products
                .Where(p => allIds.Contains(p.CategoryId) && p.IsActive)
                .GroupBy(p => p.CategoryId)
                .Select(g => new { CategoryId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.CategoryId, x => x.Count);

            var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);

            return new PagedCategoryResultDto
            {
                Categories = categories.Select(c => MapToHierarchyDto(c, productCounts)).ToList(),
                TotalCount = totalCount,
                PageNumber = query.PageNumber,
                PageSize = query.PageSize,
                TotalPages = totalPages
            };
        }


        public async Task<CategoryDto> GetCategoryByCodeAsync(string categoryCode)
        {
            var id = await _codeLookup.ResolveCategoryIdAsync(categoryCode);
            var category = await _context.Categories
                .Include(c => c.SubCategories)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
                throw new NotFoundException("Category not found");

            var productCount = await _context.Products
                .CountAsync(p => p.CategoryId == id && p.IsActive);

            return MapToDto(category, productCount);
        }

        // ── Helper queries ────────────────────────────────────────────────────

        public async Task<bool> CategoryExistsAsync(int id)
            => await _context.Categories
                .AnyAsync(c => c.Id == id && c.Status == CategoryStatus.Active);

        public async Task<bool> ValidateParentCategoryAsync(int? parentCategoryId)
        {
            if (!parentCategoryId.HasValue || parentCategoryId.Value <= 0) return true;
            return await _context.Categories
                .AnyAsync(c => c.Id == parentCategoryId && c.Status == CategoryStatus.Active);
        }


        private CategoryDto MapToDto(Category c, int productCount) => new()
        {
            Code = c.Code,
            Id = c.Id,
            NameAr = c.NameAr,
            NameEn = c.NameEn,
            Description = c.Description,
            ImageUrl = c.ImageUrl,
            ParentCategoryId = c.ParentCategoryId,
            ProductCount = productCount,
            Status = c.Status.ToString(),
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt
        };

        private CategoryHierarchyDto MapToHierarchyDto(
            Category category, Dictionary<int, int> productCounts) => new()
            {
                Code = category.Code,
                Id = category.Id,
                NameAr = category.NameAr,
                NameEn = category.NameEn,
                Description = category.Description,
                ImageUrl = category.ImageUrl,
                ParentCategoryId = category.ParentCategoryId,
                ProductCount = productCounts.GetValueOrDefault(category.Id, 0),
                Status = category.Status.ToString(),
                CreatedAt = category.CreatedAt,
                UpdatedAt = category.UpdatedAt,
                SubCategories = category.SubCategories
                .OrderBy(s => s.NameEn)
                .Select(s => MapToHierarchyDto(s, productCounts))
                .ToList()
            };

        private IEnumerable<int> GetAllIdsFromTree(Category category)
        {
            yield return category.Id;
            foreach (var sub in category.SubCategories ?? new List<Category>())
                foreach (var id in GetAllIdsFromTree(sub))
                    yield return id;
        }

        private async Task<bool> HasCircularReferenceAsync(int categoryId, int potentialParentId)
        {
            var parent = await _context.Categories
                .FirstOrDefaultAsync(c => c.Id == potentialParentId);
            while (parent != null)
            {
                if (parent.Id == categoryId) return true;
                if (parent.ParentCategoryId == null) return false;
                parent = await _context.Categories
                    .FirstOrDefaultAsync(c => c.Id == parent.ParentCategoryId);
            }
            return false;
        }

        private async Task DeactivateSubcategoriesAsync(int parentCategoryId)
        {
            var subcategories = await _context.Categories
                .Where(c => c.ParentCategoryId == parentCategoryId)
                .ToListAsync();

            foreach (var sub in subcategories)
            {
                sub.Status = CategoryStatus.Inactive;
                sub.UpdatedAt = DateTime.UtcNow;
                await DeactivateSubcategoriesAsync(sub.Id);
            }
        }
    }
}
