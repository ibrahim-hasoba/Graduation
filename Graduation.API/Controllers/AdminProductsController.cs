using Graduation.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.DTOs.Product;
using Shared.Errors;

namespace Graduation.API.Controllers
{
    [Route("api/admin/products")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminProductsController : BaseController
    {
        private readonly IProductService _productService;
        private readonly IActivityLogService _activityLog;

        public AdminProductsController(
            IProductService productService,
            IActivityLogService activityLog,
            ILanguageService lang)
            : base(lang)
        {
            _productService = productService;
            _activityLog = activityLog;
        }

        /// <summary>Gets a paginated list of all products with optional search filters (admin).</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet("")]
        public async Task<IActionResult> GetAllProducts([FromQuery] ProductSearchDto searchDto)
        {
            var result = await _productService.AdminSearchProductsAsync(searchDto);
            return OkResult(data: result);
        }

        /// <summary>Gets a single product by its numeric ID.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetProductById(int id)
        {
            var product = await _productService.GetProductByIdAsync(id);
            return OkResult(data: product);
        }

        /// <summary>Gets a single product by its alphanumeric code.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpGet("code/{code:regex(^[[A-Za-z0-9-]]{{3,}}$)}")]
        public async Task<IActionResult> GetProductByCode(string code)
        {
            var product = await _productService.GetProductByCodeAsync(code);
            return OkResult(data: product);
        }

        /// <summary>Creates a new product as an admin bypassing vendor ownership.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpPost("")]
        public async Task<IActionResult> CreateProduct([FromBody] ProductCreateDto dto)
        {
            var product = await _productService.CreateProductAsync(dto);
            await _activityLog.LogAsync(GetRequiredUserId(), "Create", "Product", product.Code, $"Created product {product.NameEn}");
            return StatusCode(201, new Errors.ApiResult(data: product, message: Lang.GetMessage(LangKeys.Product.AdminCreated)));
        }

        /// <summary>Updates any product by ID as an admin.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateProduct(int id, [FromBody] ProductUpdateDto dto)
        {
            var product = await _productService.AdminUpdateProductAsync(id, dto);
            await _activityLog.LogAsync(GetRequiredUserId(), "Update", "Product", product.Code, $"Updated product {product.NameEn}");
            return OkResult(data: product, message: Lang.GetMessage(LangKeys.Product.AdminUpdated));
        }

        /// <summary>Deletes any product by ID as an admin.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            await _activityLog.LogAsync(GetRequiredUserId(), "Delete", "Product", id.ToString(), $"Deleted product #{id}");
            await _productService.AdminDeleteProductAsync(id);
            return OkResult(message: Lang.GetMessage(LangKeys.Product.AdminDeleted));
        }

        /// <summary>Updates the stock quantity of a product.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPatch("{id:int}/stock")]
        public async Task<IActionResult> UpdateProductStock(int id, [FromBody] UpdateStockDto dto)
        {
            await _activityLog.LogAsync(GetRequiredUserId(), "UpdateStock", "Product", id.ToString(), $"Updated stock for product #{id} to {dto.Quantity}");
            await _productService.AdminUpdateStockAsync(id, dto.Quantity);
            return OkResult(message: Lang.GetMessage(LangKeys.Product.StockAdminUpdated));
        }

        /// <summary>Toggles a product's active/inactive status.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPost("{id:int}/toggle-status")]
        public async Task<IActionResult> ToggleProductStatus(int id)
        {
            var product = await _productService.AdminToggleProductStatusAsync(id);
            var action = product.IsActive ? "Activate" : "Deactivate";
            await _activityLog.LogAsync(GetRequiredUserId(), action, "Product", product.Code, $"{action}d product {product.NameEn}");
            var msg = product.IsActive ? Lang.GetMessage(LangKeys.Product.Activated) : Lang.GetMessage(LangKeys.Product.Deactivated);
            return OkResult(data: product, message: msg);
        }
    }
}
