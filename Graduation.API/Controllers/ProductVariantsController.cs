using Shared.Errors;
using Graduation.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Graduation.API.Extensions;
using Shared.DTOs.Product;

namespace Graduation.API.Controllers
{

    [Route("api/products/{productId:int}/variants")]
    [ApiController]
    public class ProductVariantsController : ControllerBase
    {
        private readonly IProductVariantService _variantService;
        private readonly IVendorService _vendorService;

        public ProductVariantsController(
            IProductVariantService variantService,
            IVendorService vendorService)
        {
            _variantService = variantService;
            _vendorService = vendorService;
        }


        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetVariants(int productId)
        {
            var groups = await _variantService.GetProductVariantsAsync(productId);
            return Ok(new ApiResult(data: groups, count: groups.Count));
        }

        [HttpGet("{variantId:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetVariantById(int productId, int variantId)
        {
            var variant = await _variantService.GetVariantByIdAsync(variantId);
            return Ok(new ApiResult(data: variant));
        }


        [HttpPost]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> AddVariant(int productId, [FromBody] CreateProductVariantDto dto)
        {
            var vendorId = await GetVendorIdAsync();
            var variant = await _variantService.AddVariantAsync(productId, vendorId, dto);
            return StatusCode(201, new ApiResult(data: variant, message: "Variant added successfully."));
        }


        [HttpPost("bulk")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> BulkUpsertVariantType(
            int productId,
            [FromBody] BulkUpsertVariantTypeDto dto)
        {
            var vendorId = await GetVendorIdAsync();
            var group = await _variantService.BulkUpsertVariantTypeAsync(productId, vendorId, dto);
            return Ok(new ApiResult(
                data: group,
                message: $"Variants for type '{group.TypeName}' updated successfully."));
        }

        [HttpPut("{variantId:int}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdateVariant(
            int productId,
            int variantId,
            [FromBody] UpdateProductVariantDto dto)
        {
            var vendorId = await GetVendorIdAsync();
            var variant = await _variantService.UpdateVariantAsync(variantId, vendorId, dto);
            return Ok(new ApiResult(data: variant, message: "Variant updated successfully."));
        }

        [HttpDelete("{variantId:int}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteVariant(int productId, int variantId)
        {
            var vendorId = await GetVendorIdAsync();
            await _variantService.DeleteVariantAsync(variantId, vendorId);
            return Ok(new ApiResult(message: "Variant removed successfully."));
        }


        [HttpDelete("type/{typeName}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteVariantType(int productId, string typeName)
        {
            var vendorId = await GetVendorIdAsync();
            await _variantService.DeleteVariantTypeAsync(productId, vendorId, typeName);
            return Ok(new ApiResult(message: $"All '{typeName}' variants removed successfully."));
        }


        private async Task<int> GetVendorIdAsync()
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                throw new UnauthorizedException("User not authenticated.");

            var vendor = await _vendorService.GetVendorByUserIdAsync(userId);
            if (vendor == null)
                throw new UnauthorizedException("You must be a vendor to manage product variants.");

            if (!vendor.IsApproved)
                throw new UnauthorizedException("Your vendor account must be approved before managing variants.");

            return vendor.Id;
        }
    }
}
