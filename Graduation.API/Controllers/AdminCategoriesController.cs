using Graduation.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.DTOs.Category;
using Shared.Errors;

namespace Graduation.API.Controllers
{
    [Route("api/admin/categories")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminCategoriesController : BaseController
    {
        private readonly ICategoryService _categoryService;
        private readonly IActivityLogService _activityLog;

        public AdminCategoriesController(
            ICategoryService categoryService,
            IActivityLogService activityLog,
            ILanguageService lang)
            : base(lang)
        {
            _categoryService = categoryService;
            _activityLog = activityLog;
        }

        /// <summary>Creates a new product category.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpPost("")]
        public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryDto dto)
        {
            var category = await _categoryService.CreateCategoryAsync(dto);
            await _activityLog.LogAsync(GetRequiredUserId(), "Create", "Category", category.Code, $"Created category {category.NameEn}");
            return CreatedAtAction(
                nameof(GetCategoryByCode),
                new { categoryCode = category.Code },
                new Errors.ApiResult(data: category));
        }

        /// <summary>Gets a paginated list of all categories with optional query filters.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet("")]
        public async Task<IActionResult> GetAllCategories([FromQuery] CategoryQueryDto query)
        {
            var result = await _categoryService.GetAllCategoriesAsync(query);
            return OkResult(
                data: new
                {
                    categories = result.Categories,
                    totalCount = result.TotalCount,
                    pageNumber = result.PageNumber,
                    pageSize = result.PageSize,
                    totalPages = result.TotalPages,
                    hasPreviousPage = result.HasPreviousPage,
                    hasNextPage = result.HasNextPage
                },
                count: result.TotalCount);
        }

        /// <summary>Gets a single category by its code.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpGet("{categoryCode}")]
        public async Task<IActionResult> GetCategoryByCode(string categoryCode)
        {
            var category = await _categoryService.GetCategoryByCodeAsync(categoryCode);
            return OkResult(data: category);
        }

        /// <summary>Updates an existing category by its code.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPut("{categoryCode}")]
        public async Task<IActionResult> UpdateCategory(
            string categoryCode, [FromBody] UpdateCategoryDto dto)
        {
            var category = await _categoryService.UpdateCategoryAsync(categoryCode, dto);
            await _activityLog.LogAsync(GetRequiredUserId(), "Update", "Category", categoryCode, $"Updated category {category.NameEn}");
            return OkResult(data: category, message: Lang.GetMessage(LangKeys.Category.Updated));
        }

        /// <summary>Toggles a category's active/inactive status.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPost("{categoryCode}/toggle-activation")]
        public async Task<IActionResult> ToggleCategoryActivation(string categoryCode)
        {
            var category = await _categoryService.ToggleActivationAsync(categoryCode);
            var action = category.Status == "Active" ? "Activate" : "Deactivate";
            await _activityLog.LogAsync(GetRequiredUserId(), action, "Category", categoryCode, $"{action}d category {category.NameEn}");
            var msg = category.Status == "Active"
                ? Lang.GetMessage(LangKeys.Category.Activated)
                : Lang.GetMessage(LangKeys.Category.Deactivated);
            return OkResult(data: category, message: msg);
        }

        /// <summary>Deletes a category by its code.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpDelete("{categoryCode}")]
        public async Task<IActionResult> DeleteCategory(string categoryCode)
        {
            await _activityLog.LogAsync(GetRequiredUserId(), "Delete", "Category", categoryCode, $"Deleted category {categoryCode}");
            await _categoryService.DeleteCategoryAsync(categoryCode);
            return OkResult(message: Lang.GetMessage(LangKeys.Category.Deleted));
        }
    }
}
