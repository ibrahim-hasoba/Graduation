using Shared.Errors;
using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.DTOs.Category;

namespace Graduation.BLL.Services.Implementations
{
    public class CategoryService : ICategoryService
    {
        private readonly DatabaseContext _context;
        private readonly ILogger<CategoryService> _logger;

        public CategoryService(DatabaseContext context, ILogger<CategoryService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<CategoryDto> CreateCategoryAsync(CreateCategoryDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.NameEn) || string.IsNullOrWhiteSpace(dto.NameAr))
                throw new BadRequestException("Category names in both English and Arabic are required");

            if (dto.ParentCategoryId.HasValue)
            {
                if (dto.ParentCategoryId.Value <= 0)
                    dto.ParentCategoryId = null;
                else
                {
                    var parentExists = await _context.Categories
                        .AnyAsync(c => c.Id == dto.ParentCategoryId && c.IsActive);
                    if (!parentExists)
                        throw new NotFoundException("Parent category not found");
                }
            }

            var duplicateName = await _context.Categories
                .AnyAsync(c =>
                    (c.NameEn.ToLower() == dto.NameEn.ToLower() || c.NameAr == dto.NameAr)
                    && c.IsActive);
            if (duplicateName)
                throw new ConflictException("Category with this name already exists");

            var category = new Category
            {
                NameEn = dto.NameEn,
                NameAr = dto.NameAr,
                Description = dto.Description,
                ImageUrl = dto.ImageUrl,
                ParentCategoryId = dto.ParentCategoryId,
                IsActive = true
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Category created: {Name} (ID: {Id})", dto.NameEn, category.Id);

            return await GetCategoryByIdAsync(category.Id);
        }

        public async Task<CategoryDto> UpdateCategoryAsync(int id, UpdateCategoryDto dto)
        {
            var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == id);
            if (category == null)
                throw new NotFoundException("Category not found");

            if (!string.IsNullOrWhiteSpace(dto.NameEn))
            {
                var dup = await _context.Categories
                    .AnyAsync(c => c.Id != id && c.NameEn.ToLower() == dto.NameEn.ToLower() && c.IsActive);
                if (dup) throw new ConflictException("Another category with this English name already exists");
                category.NameEn = dto.NameEn;
            }

            if (!string.IsNullOrWhiteSpace(dto.NameAr))
            {
                var dup = await _context.Categories
                    .AnyAsync(c => c.Id != id && c.NameAr == dto.NameAr && c.IsActive);
                if (dup) throw new ConflictException("Another category with this Arabic name already exists");
                category.NameAr = dto.NameAr;
            }

            if (dto.Description != null) category.Description = dto.Description;
            if (dto.ImageUrl != null) category.ImageUrl = dto.ImageUrl;

            int? newParentId = dto.ParentCategoryId.HasValue && dto.ParentCategoryId.Value > 0
                ? dto.ParentCategoryId : null;

            if (newParentId != category.ParentCategoryId)
            {
                if (newParentId.HasValue)
                {
                    var parentExists = await _context.Categories
                        .AnyAsync(c => c.Id == newParentId && c.IsActive);
                    if (!parentExists) throw new NotFoundException("Parent category not found");

                    if (await HasCircularReference(id, newParentId.Value))
                        throw new BusinessException("Cannot set this as parent — circular reference detected");
                }
                category.ParentCategoryId = newParentId;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Category updated: {Name} (ID: {Id})", category.NameEn, id);
            return await GetCategoryByIdAsync(id);
        }

        public async Task DeleteCategoryAsync(int id)
        {
            var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == id);
            if (category == null) throw new NotFoundException("Category not found");

            category.IsActive = false;
            await DeactivateSubcategoriesAsync(id);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Category soft-deleted: {Name} (ID: {Id})", category.NameEn, id);
        }

        public async Task<CategoryDto> GetCategoryByIdAsync(int id)
        {
            var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == id);
            if (category == null) throw new NotFoundException("Category not found");

            return await MapToCategoryDtoAsync(category);
        }

        public async Task<List<CategoryHierarchyDto>> GetAllCategoriesAsync(bool includeInactive = false)
        {
            var query = _context.Categories.AsQueryable();
            if (!includeInactive) query = query.Where(c => c.IsActive);

            var categories = await query
                .Include(c => c.SubCategories)
                .Where(c => c.ParentCategoryId == null)
                .ToListAsync();

            
            var allCategoryIds = categories
                .SelectMany(c => GetAllIds(c))
                .Distinct()
                .ToList();

            var productCounts = await _context.Products
                .Where(p => allCategoryIds.Contains(p.CategoryId) && p.IsActive)
                .GroupBy(p => p.CategoryId)
                .Select(g => new { CategoryId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.CategoryId, x => x.Count);

            return categories
                .Select(c => MapToCategoryHierarchyDto(c, productCounts))
                .OrderBy(c => c.NameEn)
                .ToList();
        }

        public async Task<bool> CategoryExistsAsync(int id)
            => await _context.Categories.AnyAsync(c => c.Id == id && c.IsActive);

        public async Task<bool> ValidateParentCategoryAsync(int? parentCategoryId)
        {
            if (!parentCategoryId.HasValue || parentCategoryId.Value <= 0) return true;
            return await _context.Categories.AnyAsync(c => c.Id == parentCategoryId && c.IsActive);
        }

        private async Task<CategoryDto> MapToCategoryDtoAsync(Category category)
        {
            var count = await _context.Products
                .CountAsync(p => p.CategoryId == category.Id && p.IsActive);

            return new CategoryDto
            {
                Id = category.Id,
                NameEn = category.NameEn,
                NameAr = category.NameAr,
                Description = category.Description,
                ImageUrl = category.ImageUrl,
                ParentCategoryId = category.ParentCategoryId,
                ProductCount = count
            };
        }

        
        private CategoryHierarchyDto MapToCategoryHierarchyDto(
            Category category,
            Dictionary<int, int> productCounts)
        {
            return new CategoryHierarchyDto
            {
                Id = category.Id,
                NameEn = category.NameEn,
                NameAr = category.NameAr,
                Description = category.Description,
                ImageUrl = category.ImageUrl,
                ParentCategoryId = category.ParentCategoryId,
                ProductCount = productCounts.GetValueOrDefault(category.Id, 0),
                SubCategories = category.SubCategories
                    .Where(s => s.IsActive)
                    .Select(s => MapToCategoryHierarchyDto(s, productCounts))
                    .OrderBy(s => s.NameEn)
                    .ToList()
            };
        }

        private IEnumerable<int> GetAllIds(Category category)
        {
            yield return category.Id;
            foreach (var sub in category.SubCategories ?? new List<Category>())
                foreach (var id in GetAllIds(sub))
                    yield return id;
        }

        private async Task<bool> HasCircularReference(int categoryId, int potentialParentId)
        {
            var parent = await _context.Categories.FirstOrDefaultAsync(c => c.Id == potentialParentId);
            while (parent != null)
            {
                if (parent.Id == categoryId) return true;
                if (parent.ParentCategoryId == null) return false;
                parent = await _context.Categories.FirstOrDefaultAsync(c => c.Id == parent.ParentCategoryId);
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
                sub.IsActive = false;
                await DeactivateSubcategoriesAsync(sub.Id);
            }
        }
    }
}
