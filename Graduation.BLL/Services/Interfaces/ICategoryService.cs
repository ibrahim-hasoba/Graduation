using Shared.DTOs.Category;

namespace Graduation.BLL.Services.Interfaces
{
        public interface ICategoryService
        {
            Task<CategoryDto> CreateCategoryAsync(CreateCategoryDto dto);
            Task<CategoryDto> UpdateCategoryAsync(string categoryCode, UpdateCategoryDto dto);
            Task<CategoryDto> ToggleActivationAsync(string categoryCode);
            Task DeleteCategoryAsync(string categoryCode);
            Task<PagedCategoryResultDto> GetAllCategoriesAsync(CategoryQueryDto query);
            Task<CategoryDto> GetCategoryByCodeAsync(string categoryCode);
            Task<bool> CategoryExistsAsync(int id);
            Task<bool> ValidateParentCategoryAsync(int? parentCategoryId);
        }
}
