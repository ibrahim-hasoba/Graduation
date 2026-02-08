using Graduation.API.Errors;
using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
        public async Task<IActionResult> SearchProducts([FromQuery] ProductSearchDto searchDto)
        {
            var result = await _productService.SearchProductsAsync(searchDto);
            return Ok(new { success = true, data = result });
        }

        /// <summary>
        /// Get all products (public)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllProducts(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var searchDto = new ProductSearchDto
            {
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            var result = await _productService.SearchProductsAsync(searchDto);
            return Ok(new { success = true, data = result });
        }

        /// <summary>
        /// Get featured products (public)
        /// </summary>
        [HttpGet("featured")]
        public async Task<IActionResult> GetFeaturedProducts([FromQuery] int count = 10)
        {
            var products = await _productService.GetFeaturedProductsAsync(count);
            return Ok(new { success = true, data = products });
        }

        /// <summary>
        /// Get product by ID (public)
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetProductById(int id)
        {
            // Increment view count
            await _productService.IncrementViewCountAsync(id);

            var product = await _productService.GetProductByIdAsync(id);
            return Ok(new { success = true, data = product });
        }

        /// <summary>
        /// Get products by vendor (public)
        /// </summary>
        [HttpGet("vendor/{vendorId}")]
        public async Task<IActionResult> GetVendorProducts(int vendorId)
        {
            var products = await _productService.GetVendorProductsAsync(vendorId);
            return Ok(new { success = true, data = products });
        }

        /// <summary>
        /// Get my products (vendor only)
        /// </summary>
        [HttpGet("my-products")]
        [Authorize]
        public async Task<IActionResult> GetMyProducts()
        {
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            // Get vendor profile
            var vendor = await _vendorService.GetVendorByUserIdAsync(userId);
            if (vendor == null)
                return NotFound(new ApiResponse(404, "You don't have a vendor account"));

            var products = await _productService.GetVendorProductsAsync(vendor.Id);
            return Ok(new { success = true, data = products });
        }

        /// <summary>
        /// Create new product (vendor only)
        /// </summary>
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateProduct([FromBody] ProductCreateDto dto)
        {
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            // Get vendor profile
            var vendor = await _vendorService.GetVendorByUserIdAsync(userId);
            if (vendor == null)
                throw new UnauthorizedException("You must be a vendor to create products");

            if (!vendor.IsApproved)
                throw new UnauthorizedException("Your vendor account must be approved before adding products");

            var product = await _productService.CreateProductAsync(vendor.Id, dto);

            return StatusCode(201, new
            {
                success = true,
                message = "Product created successfully",
                data = product
            });
        }

        /// <summary>
        /// Update product (vendor owner only)
        /// </summary>
        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> UpdateProduct(int id, [FromBody] ProductUpdateDto dto)
        {
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            var vendor = await _vendorService.GetVendorByUserIdAsync(userId);
            if (vendor == null)
                throw new UnauthorizedException("You must be a vendor to update products");

            var product = await _productService.UpdateProductAsync(id, vendor.Id, dto);

            return Ok(new
            {
                success = true,
                message = "Product updated successfully",
                data = product
            });
        }

        /// <summary>
        /// Delete product (vendor owner only)
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            var vendor = await _vendorService.GetVendorByUserIdAsync(userId);
            if (vendor == null)
                throw new UnauthorizedException("You must be a vendor to delete products");

            await _productService.DeleteProductAsync(id, vendor.Id);

            return Ok(new
            {
                success = true,
                message = "Product deleted successfully"
            });
        }

        /// <summary>
        /// Update product stock (vendor owner only)
        /// </summary>
        [HttpPatch("{id}/stock")]
        [Authorize]
        public async Task<IActionResult> UpdateStock(int id, [FromBody] UpdateStockDto dto)
        {
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            await _productService.UpdateStockAsync(id, dto.Quantity);

            return Ok(new
            {
                success = true,
                message = "Stock updated successfully"
            });
        }
    }

    public class UpdateStockDto
    {
        public int Quantity { get; set; }
    }
}
