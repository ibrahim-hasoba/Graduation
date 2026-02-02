using Shared.DTOs.Product;
using System;
using System.Collections.Generic;
using System.Text;

namespace Graduation.BLL.Services.Interfaces
{
    public interface IProductService
    {
        Task<ProductDto> CreateProductAsync(int vendorId, ProductCreateDto dto);
        Task<ProductDto> GetProductByIdAsync(int id);
        Task<ProductSearchResultDto> SearchProductsAsync(ProductSearchDto searchDto);
        Task<List<ProductListDto>> GetVendorProductsAsync(int vendorId);
        Task<List<ProductListDto>> GetFeaturedProductsAsync(int count = 10);
        Task<ProductDto> UpdateProductAsync(int id, int vendorId, ProductUpdateDto dto);
        Task DeleteProductAsync(int id, int vendorId);
        Task<bool> UpdateStockAsync(int id, int quantity);
        Task IncrementViewCountAsync(int id);
    }
}
