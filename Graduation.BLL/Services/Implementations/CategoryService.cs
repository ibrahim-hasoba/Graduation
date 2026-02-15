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

    public CategoryService(
        DatabaseContext context,
        ILogger<CategoryService> logger)
    {
      _context = context;
      _logger = logger;
    }

    public async Task<CategoryDto> CreateCategoryAsync(CreateCategoryDto dto)
    {
      // Validation
      if (string.IsNullOrWhiteSpace(dto.NameEn) || string.IsNullOrWhiteSpace(dto.NameAr))
        throw new BadRequestException("Category names in both English and Arabic are required");

      // Check if parent category exists (if provided). Treat non-positive values as "no parent".
      if (dto.ParentCategoryId.HasValue)
      {
        if (dto.ParentCategoryId.Value <= 0)
        {
          dto.ParentCategoryId = null;
        }
        else
        {
          var parentExists = await _context.Categories
              .AnyAsync(c => c.Id == dto.ParentCategoryId && c.IsActive);

          if (!parentExists)
            throw new NotFoundException("Parent category not found");
        }
      }

      // Check for duplicate name
      var duplicateName = await _context.Categories
          .AnyAsync(c =>
              (c.NameEn.ToLower() == dto.NameEn.ToLower() ||
               c.NameAr == dto.NameAr) &&
              c.IsActive);

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

      _logger.LogInformation("Category created: {CategoryName} (ID: {CategoryId})",
          dto.NameEn, category.Id);

      return await GetCategoryByIdAsync(category.Id);
    }

    public async Task<CategoryDto> UpdateCategoryAsync(int id, UpdateCategoryDto dto)
    {
      var category = await _context.Categories
          .FirstOrDefaultAsync(c => c.Id == id);

      if (category == null)
        throw new NotFoundException("Category not found");

      // Update only provided fields
      if (!string.IsNullOrWhiteSpace(dto.NameEn))
      {
        // Check for duplicate
        var duplicateName = await _context.Categories
            .AnyAsync(c =>
                c.Id != id &&
                c.NameEn.ToLower() == dto.NameEn.ToLower() &&
                c.IsActive);

        if (duplicateName)
          throw new ConflictException("Another category with this English name already exists");

        category.NameEn = dto.NameEn;
      }

      if (!string.IsNullOrWhiteSpace(dto.NameAr))
      {
        // Check for duplicate
        var duplicateName = await _context.Categories
            .AnyAsync(c =>
                c.Id != id &&
                c.NameAr == dto.NameAr &&
                c.IsActive);

        if (duplicateName)
          throw new ConflictException("Another category with this Arabic name already exists");

        category.NameAr = dto.NameAr;
      }

      if (dto.Description != null)
        category.Description = dto.Description;

      if (dto.ImageUrl != null)
        category.ImageUrl = dto.ImageUrl;

      // Handle parent category change. Treat non-positive values as null (unset parent).
      int? newParentId = dto.ParentCategoryId.HasValue && dto.ParentCategoryId.Value > 0
          ? dto.ParentCategoryId
          : null;

      if (newParentId != category.ParentCategoryId)
      {
        if (newParentId.HasValue)
        {
          var parentExists = await _context.Categories
              .AnyAsync(c => c.Id == newParentId && c.IsActive);

          if (!parentExists)
            throw new NotFoundException("Parent category not found");

          // Prevent circular reference
          if (await HasCircularReference(id, newParentId.Value))
            throw new BusinessException("Cannot set this as parent category - it would create a circular reference");
        }

        category.ParentCategoryId = newParentId;
      }

      await _context.SaveChangesAsync();

      _logger.LogInformation("Category updated: {CategoryName} (ID: {CategoryId})",
          category.NameEn, id);

      return await GetCategoryByIdAsync(id);
    }

    public async Task DeleteCategoryAsync(int id)
    {
      var category = await _context.Categories
          .FirstOrDefaultAsync(c => c.Id == id);

      if (category == null)
        throw new NotFoundException("Category not found");

      // Soft delete: set IsActive to false
      category.IsActive = false;

      // Also deactivate all subcategories
      await DeactivateSubcategoriesAsync(id);

      await _context.SaveChangesAsync();

      _logger.LogInformation("Category deleted (soft): {CategoryName} (ID: {CategoryId})",
          category.NameEn, id);
    }

    public async Task<CategoryDto> GetCategoryByIdAsync(int id)
    {
      var category = await _context.Categories
          .FirstOrDefaultAsync(c => c.Id == id);

      if (category == null)
        throw new NotFoundException("Category not found");

      return MapToCategoryDto(category);
    }

    public async Task<List<CategoryHierarchyDto>> GetAllCategoriesAsync(bool includeInactive = false)
    {
      var query = _context.Categories.AsQueryable();

      if (!includeInactive)
        query = query.Where(c => c.IsActive);

      var categories = await query
          .Include(c => c.SubCategories)
          .Where(c => c.ParentCategoryId == null)
          .ToListAsync();

      return categories
          .Select(c => MapToCategoryHierarchyDto(c))
          .OrderBy(c => c.NameEn)
          .ToList();
    }

    public async Task<bool> CategoryExistsAsync(int id)
    {
      return await _context.Categories
          .AnyAsync(c => c.Id == id && c.IsActive);
    }

    public async Task<bool> ValidateParentCategoryAsync(int? parentCategoryId)
    {
      if (!parentCategoryId.HasValue || parentCategoryId.Value <= 0)
        return true;

      return await _context.Categories
          .AnyAsync(c => c.Id == parentCategoryId && c.IsActive);
    }

    // Private helper methods
    private CategoryDto MapToCategoryDto(Category category)
    {
      return new CategoryDto
      {
        Id = category.Id,
        NameEn = category.NameEn,
        NameAr = category.NameAr,
        Description = category.Description,
        ImageUrl = category.ImageUrl,
        ParentCategoryId = category.ParentCategoryId,
        ProductCount = _context.Products
              .Count(p => p.CategoryId == category.Id && p.IsActive)
      };
    }

    private CategoryHierarchyDto MapToCategoryHierarchyDto(Category category)
    {
      return new CategoryHierarchyDto
      {
        Id = category.Id,
        NameEn = category.NameEn,
        NameAr = category.NameAr,
        Description = category.Description,
        ImageUrl = category.ImageUrl,
        ParentCategoryId = category.ParentCategoryId,
        ProductCount = _context.Products
              .Count(p => p.CategoryId == category.Id && p.IsActive),
        SubCategories = category.SubCategories
              .Where(s => s.IsActive)
              .Select(s => MapToCategoryHierarchyDto(s))
              .OrderBy(s => s.NameEn)
              .ToList()
      };
    }

    private async Task<bool> HasCircularReference(int categoryId, int potentialParentId)
    {
      var parent = await _context.Categories
          .FirstOrDefaultAsync(c => c.Id == potentialParentId);

      while (parent != null)
      {
        if (parent.Id == categoryId)
          return true;

        if (parent.ParentCategoryId == null)
          return false;

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
        sub.IsActive = false;
        await DeactivateSubcategoriesAsync(sub.Id);
      }
    }
  }
}
