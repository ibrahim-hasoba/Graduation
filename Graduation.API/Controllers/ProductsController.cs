using Shared.Errors;
using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Graduation.API.Extensions;
using Shared.DTOs.Product;

namespace Graduation.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _productService;
        private readonly IVendorService _vendorService;
        private readonly DatabaseContext _context;

        public ProductsController(
            IProductService productService,
            IVendorService vendorService,
            DatabaseContext context)
        {
            _productService = productService;
            _vendorService = vendorService;
            _context = context;
        }

        /// <summary>
        /// Search and filter products (public)
        /// </summary>
        [HttpGet("search")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> SearchProducts([FromQuery] ProductSearchDto searchDto)
        {
            var result = await _productService.SearchProductsAsync(searchDto);
            return Ok(new ApiResult(data: result));
        }

        /// <summary>
        /// Get all products (public)
        /// </summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllProducts(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _productService.SearchProductsAsync(new ProductSearchDto
            {
                PageNumber = pageNumber,
                PageSize = pageSize
            });
            return Ok(new ApiResult(data: result));
        }

        /// <summary>
        /// Get featured products (public)
        /// </summary>
        [HttpGet("featured")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetFeaturedProducts(
                [FromQuery] int pageNumber = 1,
                [FromQuery] int pageSize = 10)
        {
            var result = await _productService.GetFeaturedProductsAsync(pageNumber, pageSize);
            return Ok(new ApiResult(data: result));
        }

        /// <summary>
        /// Get product by numeric id 
        /// </summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetProductById(int id)
        {
            var product = await _productService.GetProductByIdAsync(id);
            await _productService.IncrementViewCountAsync(product.Id);
            return Ok(new ApiResult(data: product));
        }

        /// <summary>
        /// Get product by code 
        /// </summary>
        [HttpGet("{code:regex(^[[A-Za-z0-9-]]{{3,}}$)}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetProductByCode(string code)
        {
            var product = await _productService.GetProductByCodeAsync(code);
            await _productService.IncrementViewCountAsync(product.Id);
            return Ok(new ApiResult(data: product));
        }

        [HttpGet("vendor/{vendorId:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetVendorProducts(
                 int vendorId,
                [FromQuery] int pageNumber = 1,
                [FromQuery] int pageSize = 20)
        {
            var products = await _productService.GetVendorProductsAsync(vendorId, pageNumber, pageSize);
            return Ok(new ApiResult(data: products));
        }

        /// <summary>
        /// Get my products (vendor only)
        /// </summary>
        [HttpGet("my-products")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetMyProducts(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            var vendor = await _vendorService.GetVendorByUserIdAsync(userId);
            if (vendor == null)
                return NotFound(new ApiResponse(404, "You don't have a vendor account"));

            var result = await _productService.GetVendorProductsAsync(vendor.Id, pageNumber, pageSize);
            return Ok(new ApiResult(data: result));
        }

        /// <summary>
        /// Create new product (vendor only)
        /// </summary>
        [HttpPost]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateProduct([FromBody] ProductCreateDto dto)
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            var product = await _productService.CreateProductAsync(dto);
            return StatusCode(201, new ApiResult(data: product, message: "Product created successfully"));
        }

        /// <summary>
        /// Update product by id (vendor owner only)
        /// </summary>
        [HttpPut("{id:int}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateProduct(int id, [FromBody] ProductUpdateDto dto)
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            var vendor = await _vendorService.GetVendorByUserIdAsync(userId);
            if (vendor == null)
                throw new UnauthorizedException("You must be a vendor to update products");

            var product = await _productService.UpdateProductAsync(id, vendor.Code, dto);
            return Ok(new ApiResult(data: product, message: "Product updated successfully"));
        }

        /// <summary>
        /// Delete product by id (vendor owner only)
        /// </summary>
        [HttpDelete("{id:int}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            var vendor = await _vendorService.GetVendorByUserIdAsync(userId);
            if (vendor == null)
                throw new UnauthorizedException("You must be a vendor to delete products");

            await _productService.DeleteProductAsync(id, vendor.Code);
            return Ok(new ApiResult(message: "Product deleted successfully"));
        }

        /// <summary>
        /// Update stock by id (vendor owner only)
        /// </summary>
        [HttpPatch("{id:int}/stock")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateStock(int id, [FromBody] UpdateStockDto dto)
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            var vendor = await _vendorService.GetVendorByUserIdAsync(userId);
            if (vendor == null)
                throw new UnauthorizedException("You must be a vendor to update product stock");

            await _productService.UpdateStockAsync(id, dto.Quantity, vendor.Id);
            return Ok(new ApiResult(message: "Stock updated successfully"));
        }
    }

    public class UpdateStockDto
    {
        public int Quantity { get; set; }
    }
}