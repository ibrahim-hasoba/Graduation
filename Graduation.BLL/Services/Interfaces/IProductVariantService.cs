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
        
            Task<List<ProductVariantGroupDto>> GetProductVariantsAsync(int productId);

            Task<ProductVariantDto> GetVariantByIdAsync(int variantId);

            Task<ProductVariantDto> AddVariantAsync(
                int productId,
                int? vendorId,
                bool isAdmin,
                CreateProductVariantDto dto);

            Task<ProductVariantGroupDto> BulkUpsertVariantTypeAsync(
                int productId,
                int? vendorId,
                bool isAdmin,
                BulkUpsertVariantTypeDto dto);

            Task<ProductVariantDto> UpdateVariantAsync(
                int variantId,
                int? vendorId,
                bool isAdmin,
                UpdateProductVariantDto dto);

            Task DeleteVariantAsync(
                int variantId,
                int? vendorId,
                bool isAdmin);

            Task DeleteVariantTypeAsync(
                int productId,
                int? vendorId,
                bool isAdmin,
                string typeName);
        
    }
}
