using Shared.DTOs.Category;

namespace Graduation.BLL.Services.Interfaces
{
  public interface ICategoryService
  {
    Task<CategoryDto> CreateCategoryAsync(CreateCategoryDto dto);
    Task<CategoryDto> UpdateCategoryAsync(int id, UpdateCategoryDto dto);
    Task DeleteCategoryAsync(int id);
    Task<CategoryDto> GetCategoryByIdAsync(int id);
    Task<List<CategoryHierarchyDto>> GetAllCategoriesAsync(bool includeInactive = false);
    Task<bool> CategoryExistsAsync(int id);
    Task<bool> ValidateParentCategoryAsync(int? parentCategoryId);
  }
}
