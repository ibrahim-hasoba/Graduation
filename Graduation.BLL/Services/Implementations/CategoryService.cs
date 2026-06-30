using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Graduation.BLL.DTOs.Category;
using Graduation.BLL.Errors;

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
                category.Status = CategoryStatus.Inactive;
                await DeactivateSubcategoriesAsync(id);
            }
            else
            {
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
            var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
                throw new NotFoundException("Category not found");

            var allIdsToDelete = await GetAllSubcategoryIdsAsync(id);
            allIdsToDelete.Add(id);

            var hasProducts = await _context.Products
                .AnyAsync(p => allIdsToDelete.Contains(p.CategoryId));

            if (hasProducts)
                throw new BadRequestException(
                    $"Cannot delete category '{category.NameEn}' or its subcategories. " +
                    "There are products still assigned to this category tree. Reassign them first.");

            var categoriesToDelete = await _context.Categories
                .Where(c => allIdsToDelete.Contains(c.Id))
                .ToListAsync();

            _context.Categories.RemoveRange(categoriesToDelete);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Category and {Count} subcategories deleted. Parent: {Name}",
                categoriesToDelete.Count - 1, category.NameEn);
        }

        private async Task<List<int>> GetAllSubcategoryIdsAsync(int parentId)
        {
            var all = await _context.Categories
                .Select(c => new { c.Id, c.ParentCategoryId })
                .AsNoTracking()
                .ToListAsync();

            var lookup = all
                .GroupBy(c => c.ParentCategoryId)
                .ToDictionary(g => g.Key ?? -1, g => g.Select(c => c.Id).ToList());

            var results = new List<int>();
            CollectDescendantIds(parentId, lookup, results);
            return results;
        }

        private static void CollectDescendantIds(int parentId, Dictionary<int, List<int>> lookup, List<int> results)
        {
            if (!lookup.TryGetValue(parentId, out var children))
                return;

            foreach (var childId in children)
            {
                results.Add(childId);
                CollectDescendantIds(childId, lookup, results);
            }
        }

        public async Task<PagedCategoryResultDto> GetAllCategoriesAsync(CategoryQueryDto query)
        {
            if (query.PageNumber < 1) query.PageNumber = 1;
            if (query.PageSize < 1 || query.PageSize > 100) query.PageSize = 20;

            var dbQuery = _context.Categories
                .Include(c => c.SubCategories)
                    .ThenInclude(s => s.SubCategories)
                        .ThenInclude(ss => ss.SubCategories)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                if (Enum.TryParse<CategoryStatus>(query.Status, ignoreCase: true, out var statusEnum))
                    dbQuery = dbQuery.Where(c => c.Status == statusEnum);
            }

            if (query.ParentId.HasValue)
                dbQuery = query.ParentId.Value == 0
                    ? dbQuery.Where(c => c.ParentCategoryId == null)
                    : dbQuery.Where(c => c.ParentCategoryId == query.ParentId.Value);
            else
                dbQuery = dbQuery.Where(c => c.ParentCategoryId == null);

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
                    .ThenInclude(s => s.SubCategories)
                        .ThenInclude(ss => ss.SubCategories)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
                throw new NotFoundException("Category not found");

            var allIds = GetAllIdsFromTree(category).ToList();
            var productCounts = await _context.Products
                .Where(p => allIds.Contains(p.CategoryId) && p.IsActive)
                .GroupBy(p => p.CategoryId)
                .Select(g => new { CategoryId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.CategoryId, x => x.Count);

            var totalCount = allIds.Sum(id => productCounts.GetValueOrDefault(id, 0));
            return MapToDto(category, totalCount, productCounts);
        }

        public async Task<bool> CategoryExistsAsync(int id)
            => await _context.Categories
                .AnyAsync(c => c.Id == id && c.Status == CategoryStatus.Active);

        public async Task<bool> ValidateParentCategoryAsync(int? parentCategoryId)
        {
            if (!parentCategoryId.HasValue || parentCategoryId.Value <= 0) return true;
            return await _context.Categories
                .AnyAsync(c => c.Id == parentCategoryId && c.Status == CategoryStatus.Active);
        }

        private CategoryDto MapToDto(Category c, int productCount, Dictionary<int, int>? productCounts = null) => new()
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
            UpdatedAt = c.UpdatedAt,
            SubCategories = c.SubCategories?
                .Select(s => MapToDto(
                    s,
                    productCounts?.GetValueOrDefault(s.Id, 0) ?? 0,
                    productCounts))
                .ToList() ?? new()
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
                ProductCount = GetTotalProductCount(category, productCounts),
                Status = category.Status.ToString(),
                CreatedAt = category.CreatedAt,
                UpdatedAt = category.UpdatedAt,
                SubCategories = category.SubCategories
                .OrderBy(s => s.NameEn)
                .Select(s => MapToHierarchyDto(s, productCounts))
                .ToList()
            };

        private static int GetTotalProductCount(Category category, Dictionary<int, int> productCounts)
        {
            var count = productCounts.GetValueOrDefault(category.Id, 0);
            foreach (var sub in category.SubCategories ?? new List<Category>())
                count += GetTotalProductCount(sub, productCounts);
            return count;
        }

        private IEnumerable<int> GetAllIdsFromTree(Category category)
        {
            yield return category.Id;
            foreach (var sub in category.SubCategories ?? new List<Category>())
                foreach (var id in GetAllIdsFromTree(sub))
                    yield return id;
        }

        private async Task<bool> HasCircularReferenceAsync(int categoryId, int potentialParentId)
        {
            var all = await _context.Categories
                .Select(c => new { c.Id, c.ParentCategoryId })
                .AsNoTracking()
                .ToListAsync();

            var parentMap = all
                .Where(c => c.ParentCategoryId.HasValue)
                .ToDictionary(c => c.Id, c => c.ParentCategoryId!.Value);

            var current = potentialParentId;
            while (parentMap.TryGetValue(current, out var parent))
            {
                if (parent == categoryId) return true;
                current = parent;
            }
            return false;
        }

        private async Task DeactivateSubcategoriesAsync(int parentCategoryId)
        {
            var allSubIds = await GetAllSubcategoryIdsAsync(parentCategoryId);
            if (allSubIds.Count == 0) return;

            await _context.Categories
                .Where(c => allSubIds.Contains(c.Id))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(c => c.Status, CategoryStatus.Inactive)
                    .SetProperty(c => c.UpdatedAt, DateTime.UtcNow));
        }
    }
}
