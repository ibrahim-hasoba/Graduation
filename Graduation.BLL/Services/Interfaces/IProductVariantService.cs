using Shared.DTOs.Product;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Graduation.BLL.Services.Interfaces
{
    public interface IProductVariantService
    {
        /// <summary>Get all variant groups for a product (public)</summary>
        Task<List<ProductVariantGroupDto>> GetProductVariantsAsync(int productId);

        /// <summary>Get a single variant by its ID</summary>
        Task<ProductVariantDto> GetVariantByIdAsync(int variantId);

        /// <summary>Add a single variant option to a product (vendor only)</summary>
        Task<ProductVariantDto> AddVariantAsync(int productId, int vendorId, CreateProductVariantDto dto);

        /// <summary>
        /// Bulk upsert: replace all options for a given type on a product.
        /// Existing options not in the new list are soft-deleted (IsActive=false).
        /// </summary>
        Task<ProductVariantGroupDto> BulkUpsertVariantTypeAsync(
            int productId, int vendorId, BulkUpsertVariantTypeDto dto);

        /// <summary>Update a single variant option (vendor only)</summary>
        Task<ProductVariantDto> UpdateVariantAsync(int variantId, int vendorId, UpdateProductVariantDto dto);

        /// <summary>Soft-delete a single variant option (vendor only)</summary>
        Task DeleteVariantAsync(int variantId, int vendorId);

        /// <summary>Delete all variants of a specific type for a product (vendor only)</summary>
        Task DeleteVariantTypeAsync(int productId, int vendorId, string typeName);
    }
}
