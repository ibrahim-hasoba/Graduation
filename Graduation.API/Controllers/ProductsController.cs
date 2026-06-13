using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Graduation.API.Extensions;
using Shared.DTOs.Product;

namespace Graduation.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController : BaseController
    {
        private readonly IProductService _productService;
        private readonly IVendorService _vendorService;
        private readonly DatabaseContext _context;

        public ProductsController(
            IProductService productService,
            IVendorService vendorService,
            DatabaseContext context,
            ILanguageService lang)
            : base(lang)
        {
            _productService = productService;
            _vendorService = vendorService;
            _context = context;
        }
        /// <summary>Searches and filters products using the provided criteria.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [HttpGet("search")]
        public async Task<IActionResult> SearchProducts([FromQuery] ProductSearchDto searchDto)
        {
            var result = await _productService.SearchProductsAsync(searchDto);
            return OkResult(data: result);
        }
        /// <summary>Gets a paginated list of all active products.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [HttpGet]
        public async Task<IActionResult> GetAllProducts(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _productService.SearchProductsAsync(new ProductSearchDto
            {
                PageNumber = pageNumber,
                PageSize = pageSize
            });
            return OkResult(data: result);
        }
        /// <summary>Gets a paginated list of featured products.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [HttpGet("featured")]
        public async Task<IActionResult> GetFeaturedProducts(
                [FromQuery] int pageNumber = 1,
                [FromQuery] int pageSize = 10)
        {
            var result = await _productService.GetFeaturedProductsAsync(pageNumber, pageSize);
            return OkResult(data: result);
        }
        /// <summary>Gets a single product by its numeric ID.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetProductById(int id)
        {
            var product = await _productService.GetProductByIdAsync(id);
            await _productService.IncrementViewCountAsync(product.Id);
            return OkResult(data: product);
        }
        /// <summary>Gets a single product by its alphanumeric code.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpGet("{code:regex(^[[A-Za-z0-9-]]{{3,}}$)}")]
        public async Task<IActionResult> GetProductByCode(string code)
        {
            var product = await _productService.GetProductByCodeAsync(code);
            await _productService.IncrementViewCountAsync(product.Id);
            return OkResult(data: product);
        }
        /// <summary>Gets a paginated list of products for a specific vendor.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpGet("vendor/{vendorId:int}")]
        public async Task<IActionResult> GetVendorProducts(
                 int vendorId,
                [FromQuery] int pageNumber = 1,
                [FromQuery] int pageSize = 20)
        {
            var products = await _productService.GetVendorProductsAsync(vendorId, pageNumber, pageSize);
            return OkResult(data: products);
        }
        /// <summary>Gets the authenticated vendor's own products with pagination.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [HttpGet("my-products")]
        [Authorize]
        public async Task<IActionResult> GetMyProducts(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var userId = GetRequiredUserId();
            var vendor = await _vendorService.GetVendorByUserIdAsync(userId);
            if (vendor == null)
                return NotFound(new Shared.Errors.ApiResponse(404, Lang.GetMessage(LangKeys.Product.NoVendor)));

            var result = await _productService.GetVendorProductsAsync(vendor.Id, pageNumber, pageSize);
            return OkResult(data: result);
        }
        /// <summary>Creates a new product for the authenticated vendor.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateProduct([FromBody] ProductCreateDto dto)
        {
            var userId = GetRequiredUserId();
            var product = await _productService.CreateProductAsync(dto);
            return CreatedResult(data: product, message: Lang.GetMessage(LangKeys.Product.Created));
        }
        /// <summary>Updates an existing product owned by the authenticated vendor.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPut("{id:int}")]
        [Authorize]
        public async Task<IActionResult> UpdateProduct(int id, [FromBody] ProductUpdateDto dto)
        {
            var userId = GetRequiredUserId();
            var vendor = await _vendorService.GetVendorByUserIdAsync(userId);
            if (vendor == null)
                throw new Shared.Errors.UnauthorizedException(Lang.GetMessage(LangKeys.Product.NotVendor));

            var product = await _productService.UpdateProductAsync(id, vendor.Code!, dto);
            return OkResult(data: product, message: Lang.GetMessage(LangKeys.Product.Updated));
        }
        /// <summary>Deletes a product owned by the authenticated vendor.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpDelete("{id:int}")]
        [Authorize]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var userId = GetRequiredUserId();
            var vendor = await _vendorService.GetVendorByUserIdAsync(userId);
            if (vendor == null)
                throw new Shared.Errors.UnauthorizedException(Lang.GetMessage(LangKeys.Product.DeleteNotVendor));

            await _productService.DeleteProductAsync(id, vendor.Code!);
            return OkResult(message: Lang.GetMessage(LangKeys.Product.Deleted));
        }
        /// <summary>Updates the stock quantity of a product owned by the authenticated vendor.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPatch("{id:int}/stock")]
        [Authorize]
        public async Task<IActionResult> UpdateStock(int id, [FromBody] UpdateStockDto dto)
        {
            var userId = GetRequiredUserId();
            var vendor = await _vendorService.GetVendorByUserIdAsync(userId);
            if (vendor == null)
                throw new Shared.Errors.UnauthorizedException(Lang.GetMessage(LangKeys.Product.StockNotVendor));

            await _productService.UpdateStockAsync(id, dto.Quantity, vendor.Id);
            return OkResult(message: Lang.GetMessage(LangKeys.Product.StockUpdated));
        }
    }

    public class UpdateStockDto
    {
        public int Quantity { get; set; }
    }
}
