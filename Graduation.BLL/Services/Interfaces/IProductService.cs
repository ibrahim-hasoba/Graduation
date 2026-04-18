using Graduation.DAL.Entities;
using Shared.DTOs.Product;
using System;
using System.Collections.Generic;
using System.Text;

namespace Graduation.BLL.Services.Interfaces
{
    public interface IProductService
    {
        Task<ProductDto> CreateProductAsync(ProductCreateDto dto);
        Task<ProductDto> GetProductByIdAsync(string code);
        Task<ProductSearchResultDto> SearchProductsAsync(ProductSearchDto searchDto);
        Task<List<ProductListDto>> GetVendorProductsAsync(int vendorId);
        Task<List<ProductListDto>> GetFeaturedProductsAsync(int count = 10);
        Task<ProductDto> UpdateProductAsync(string code, string vendorCode, ProductUpdateDto dto);
        Task DeleteProductAsync(string code, string vendorCode);
        Task<bool> UpdateStockAsync(int id, int quantity, int? vendorId = null);
        Task IncrementViewCountAsync(int id);

        Task<ProductDto> AdminUpdateProductAsync(string code, ProductUpdateDto dto);
        Task AdminDeleteProductAsync(string Code);
        Task AdminUpdateStockAsync(string code, int quantity);
        Task<ProductDto> AdminToggleProductStatusAsync(string code);
        // Task<ProductDto> AdminChangeProductStatusAsync(int id, ProductStatus newStatus);
    }
}
