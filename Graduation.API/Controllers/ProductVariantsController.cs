using Graduation.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Graduation.API.Extensions;
using Shared.DTOs.Product;

namespace Graduation.API.Controllers
{
    [Route("api/products/{productId:int}/variants")]
    [ApiController]
    public class ProductVariantsController : BaseController
    {
        private readonly IProductVariantService _variantService;
        private readonly IVendorService _vendorService;

        public ProductVariantsController(
            IProductVariantService variantService,
            IVendorService vendorService,
            ILanguageService lang)
            : base(lang)
        {
            _variantService = variantService;
            _vendorService = vendorService;
        }
        /// <summary>Gets all variant groups for a specific product.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [HttpGet]
        public async Task<IActionResult> GetVariants(int productId)
        {
            var groups = await _variantService.GetProductVariantsAsync(productId);
            return OkResult(data: groups, count: groups.Count);
        }
        /// <summary>Gets a single variant by its ID.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpGet("{variantId:int}")]
        public async Task<IActionResult> GetVariantById(int productId, int variantId)
        {
            var variant = await _variantService.GetVariantByIdAsync(variantId);
            return OkResult(data: variant);
        }
        /// <summary>Adds a new variant to a product.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [HttpPost]
        [Authorize(Roles = "Admin,Vendor")]
        public async Task<IActionResult> AddVariant(int productId, [FromBody] CreateProductVariantDto dto)
        {
            var (vendorId, isAdmin) = await GetUserContextAsync();
            var variant = await _variantService.AddVariantAsync(productId, vendorId, isAdmin, dto);
            return CreatedResult(data: variant, message: Lang.GetMessage(LangKeys.Variant.Added));
        }
        /// <summary>Bulk creates or updates all variants of a specific type for a product.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [HttpPost("bulk")]
        [Authorize(Roles = "Admin,Vendor")]
        public async Task<IActionResult> BulkUpsertVariantType(
            int productId,
            [FromBody] BulkUpsertVariantTypeDto dto)
        {
            var (vendorId, isAdmin) = await GetUserContextAsync();
            var group = await _variantService.BulkUpsertVariantTypeAsync(productId, vendorId, isAdmin, dto);
            return OkResult(
                data: group,
                message: Lang.GetMessage(LangKeys.Variant.TypeUpdated, group.TypeName));
        }
        /// <summary>Updates a specific variant.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPut("{variantId:int}")]
        [Authorize(Roles = "Admin,Vendor")]
        public async Task<IActionResult> UpdateVariant(
            int productId,
            int variantId,
            [FromBody] UpdateProductVariantDto dto)
        {
            var (vendorId, isAdmin) = await GetUserContextAsync();
            var variant = await _variantService.UpdateVariantAsync(variantId, vendorId, isAdmin, dto);
            return OkResult(data: variant, message: Lang.GetMessage(LangKeys.Variant.Updated));
        }
        /// <summary>Deletes a specific variant by its ID.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpDelete("{variantId:int}")]
        [Authorize(Roles = "Admin,Vendor")]
        public async Task<IActionResult> DeleteVariant(int productId, int variantId)
        {
            var (vendorId, isAdmin) = await GetUserContextAsync();
            await _variantService.DeleteVariantAsync(variantId, vendorId, isAdmin);
            return OkResult(message: Lang.GetMessage(LangKeys.Variant.Deleted));
        }
        /// <summary>Deletes an entire variant type group from a product.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpDelete("type/{typeName}")]
        [Authorize(Roles = "Admin,Vendor")]
        public async Task<IActionResult> DeleteVariantType(int productId, string typeName)
        {
            var (vendorId, isAdmin) = await GetUserContextAsync();
            await _variantService.DeleteVariantTypeAsync(productId, vendorId, isAdmin, typeName);
            return OkResult(message: Lang.GetMessage(LangKeys.Variant.TypeDeleted, typeName));
        }

        private async Task<(int? vendorId, bool isAdmin)> GetUserContextAsync()
        {
            var userId = GetRequiredUserId();

            if (User.IsInRole("Admin"))
                return (null, true);

            var vendor = await _vendorService.GetVendorByUserIdAsync(userId);
            if (vendor == null)
                throw new Shared.Errors.UnauthorizedException(Lang.GetMessage(LangKeys.Variant.NotVendor));

            if (!vendor.IsApproved)
                throw new Shared.Errors.UnauthorizedException(Lang.GetMessage(LangKeys.Variant.VendorNotApproved));

            return (vendor.Id, false);
        }
    }
}