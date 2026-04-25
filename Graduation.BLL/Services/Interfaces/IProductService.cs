using Graduation.DAL.Entities;
using Shared.DTOs;
using Shared.DTOs.Product;

namespace Graduation.BLL.Services.Interfaces
{
    public interface IProductService
    {
        Task<ProductDto> CreateProductAsync(ProductCreateDto dto);

        Task<ProductDto> GetProductByIdAsync(int id);
        Task<ProductDto> GetProductByCodeAsync(string code);

        Task<ProductSearchResultDto> SearchProductsAsync(ProductSearchDto searchDto);
        Task<PagedResult<ProductListDto>> GetVendorProductsAsync(int vendorId, int pageNumber = 1, int pageSize = 20);
        Task<PagedResult<ProductListDto>> GetFeaturedProductsAsync(int pageNumber = 1, int pageSize = 10);

        // Write operations — all by int id
        Task<ProductDto> UpdateProductAsync(int id, string vendorCode, ProductUpdateDto dto);
        Task DeleteProductAsync(int id, string vendorCode);
        Task<bool> UpdateStockAsync(int id, int quantity, int? vendorId = null);
        Task IncrementViewCountAsync(int id);

        // Admin operations — all by int id
        Task<ProductDto> AdminUpdateProductAsync(int id, ProductUpdateDto dto);
        Task AdminDeleteProductAsync(int id);
        Task AdminUpdateStockAsync(int id, int quantity);
        Task<ProductDto> AdminToggleProductStatusAsync(int id);
    }
}