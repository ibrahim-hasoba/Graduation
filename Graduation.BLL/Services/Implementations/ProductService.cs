using Shared.Errors;
using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs.Product;

namespace Graduation.BLL.Services.Implementations
{
    public class ProductService : IProductService
    {
        private readonly DatabaseContext _context;
        private readonly ICodeAssignmentService _codeAssignment;

        public ProductService(DatabaseContext context, ICodeAssignmentService codeAssignment)
        {
            _context = context;
            _codeAssignment = codeAssignment;
        }

        public async Task<ProductDto> CreateProductAsync(ProductCreateDto dto)
        {
            var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.Code == dto.Code);
            if (vendor == null)
                throw new NotFoundException($"Vendor with code '{dto.Code}' was not found");

            if (!vendor.IsApproved || !vendor.IsActive)
                throw new UnauthorizedException("Vendor is not approved or inactive");

            var skuExists = await _context.Products.AnyAsync(p => p.SKU == dto.SKU);
            if (skuExists)
                throw new ConflictException($"Product with SKU '{dto.SKU}' already exists");

            var category = await _context.Categories
                .Include(c => c.SubCategories)
                .FirstOrDefaultAsync(c => c.Id == dto.CategoryId && c.Status == CategoryStatus.Active);

            if (category == null)
                throw new NotFoundException("Category", "id", dto.CategoryId);

            if (category.SubCategories.Any())
                throw new BadRequestException("Products must be added to a subcategory, not a parent category");

            if (dto.IsEgyptianMade && string.IsNullOrEmpty(dto.MadeInCity))
                throw new BadRequestException("Please specify which Egyptian city this product is made in");

            if (dto.DiscountPrice.HasValue && dto.DiscountPrice >= dto.Price)
                throw new BadRequestException("Discount price must be less than regular price");

            var product = new Product
            {
                NameAr = dto.NameAr,
                NameEn = dto.NameEn,
                DescriptionAr = dto.DescriptionAr,
                DescriptionEn = dto.DescriptionEn,
                Price = dto.Price,
                DiscountPrice = dto.DiscountPrice,
                StockQuantity = dto.StockQuantity,
                SKU = dto.SKU,
                Code = dto.Code,
                CategoryId = dto.CategoryId,
                VendorId = vendor.Id,
                IsEgyptianMade = dto.IsEgyptianMade,
                MadeInCity = dto.MadeInCity,
                MadeInGovernorate = dto.MadeInGovernorate,
                IsFeatured = dto.IsFeatured,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            if (dto.ImageUrls != null && dto.ImageUrls.Any())
            {
                for (int i = 0; i < dto.ImageUrls.Count; i++)
                {
                    _context.ProductImages.Add(new ProductImage
                    {
                        ProductId = product.Id,
                        ImageUrl = dto.ImageUrls[i],
                        IsPrimary = i == 0,
                        DisplayOrder = i
                    });
                }
                await _context.SaveChangesAsync();
            }

            return await GetProductByIdAsync(product.Code);
        }

        // FIX: Interface uses string code — was int id before
        public async Task<ProductDto> GetProductByIdAsync(string code)
        {
            var product = await _context.Products
                .Include(p => p.Vendor)
                .Include(p => p.Category)
                .Include(p => p.Images.OrderBy(i => i.DisplayOrder))
                .Include(p => p.Reviews.Where(r => r.IsApproved))
                .Include(p => p.Variants.Where(v => v.IsActive))
                .FirstOrDefaultAsync(p => p.Code == code);

            if (product == null)
                throw new NotFoundException("Product", "Code", code);

            return MapToDto(product);
        }

        public async Task<ProductSearchResultDto> SearchProductsAsync(ProductSearchDto searchDto)
        {
            var query = _context.Products
                .Include(p => p.Vendor)
                .Include(p => p.Category)
                .Include(p => p.Images)
                .Include(p => p.Reviews.Where(r => r.IsApproved))
                .Where(p => p.IsActive)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchDto.SearchTerm))
            {
                var searchLower = searchDto.SearchTerm.ToLower();
                query = query.Where(p =>
                    p.NameEn.ToLower().Contains(searchLower) ||
                    p.NameAr.Contains(searchDto.SearchTerm) ||
                    p.DescriptionEn.ToLower().Contains(searchLower) ||
                    p.DescriptionAr.Contains(searchDto.SearchTerm));
            }

            if (searchDto.CategoryId.HasValue)
                query = query.Where(p => p.CategoryId == searchDto.CategoryId.Value);

            if (searchDto.VendorId.HasValue)
                query = query.Where(p => p.VendorId == searchDto.VendorId.Value);

            if (searchDto.MinPrice.HasValue)
                query = query.Where(p => p.Price >= searchDto.MinPrice.Value);

            if (searchDto.MaxPrice.HasValue)
                query = query.Where(p => p.Price <= searchDto.MaxPrice.Value);

            if (searchDto.IsEgyptianMade.HasValue)
                query = query.Where(p => p.IsEgyptianMade == searchDto.IsEgyptianMade.Value);

            if (searchDto.GovernorateId.HasValue)
                query = query.Where(p => p.MadeInGovernorate == (EgyptianGovernorate)searchDto.GovernorateId.Value);

            if (searchDto.InStock == true)
                query = query.Where(p => p.StockQuantity > 0);

            if (searchDto.IsFeatured == true)
                query = query.Where(p => p.IsFeatured);

            if (searchDto.CreatedAfter.HasValue)
                query = query.Where(p => p.CreatedAt >= searchDto.CreatedAfter.Value);

            if (searchDto.CreatedBefore.HasValue)
                query = query.Where(p => p.CreatedAt <= searchDto.CreatedBefore.Value);

            query = searchDto.SortBy?.ToLower() switch
            {
                "price_asc" => query.OrderBy(p => p.Price),
                "price_desc" => query.OrderByDescending(p => p.Price),
                "rating" => query.OrderByDescending(p => p.Reviews.Any() ? p.Reviews.Average(r => r.Rating) : 0),
                "popular" => query.OrderByDescending(p => p.ViewCount),
                "newest" => query.OrderByDescending(p => p.CreatedAt),
                _ => query.OrderByDescending(p => p.CreatedAt)
            };

            var totalCount = await query.CountAsync();

            var products = await query
                .Skip((searchDto.PageNumber - 1) * searchDto.PageSize)
                .Take(searchDto.PageSize)
                .ToListAsync();

            var totalPages = (int)Math.Ceiling(totalCount / (double)searchDto.PageSize);

            return new ProductSearchResultDto
            {
                Products = products.Select(MapToListDto).ToList(),
                TotalCount = totalCount,
                PageNumber = searchDto.PageNumber,
                PageSize = searchDto.PageSize,
                TotalPages = totalPages,
                HasPreviousPage = searchDto.PageNumber > 1,
                HasNextPage = searchDto.PageNumber < totalPages
            };
        }

        public async Task<List<ProductListDto>> GetVendorProductsAsync(int vendorId)
        {
            var products = await _context.Products
                .Include(p => p.Vendor)
                .Include(p => p.Category)
                .Include(p => p.Images)
                .Include(p => p.Reviews.Where(r => r.IsApproved))
                .Where(p => p.VendorId == vendorId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return products.Select(MapToListDto).ToList();
        }

        public async Task<List<ProductListDto>> GetFeaturedProductsAsync(int count = 10)
        {
            var products = await _context.Products
                .Include(p => p.Vendor)
                .Include(p => p.Category)
                .Include(p => p.Images)
                .Include(p => p.Reviews.Where(r => r.IsApproved))
                .Where(p => p.IsFeatured && p.IsActive && p.StockQuantity > 0)
                .OrderByDescending(p => p.ViewCount)
                .Take(count)
                .ToListAsync();

            return products.Select(MapToListDto).ToList();
        }

        // FIX: Interface signature is (string code, string vendorCode, ...).
        // FIX: Ownership check now correctly compares vendor's Code, not product's Code.
        public async Task<ProductDto> UpdateProductAsync(string code, string vendorCode, ProductUpdateDto dto)
        {
            var product = await _context.Products
                .Include(p => p.Vendor)
                .FirstOrDefaultAsync(p => p.Code == code);

            if (product == null)
                throw new NotFoundException("Product", "Code", code);

            // FIX: Compare the vendor's own Code against vendorCode, not the product's Code
            if (product.Vendor.Code != vendorCode)
                throw new UnauthorizedException("You can only update your own products");

            await ApplyProductUpdate(product, dto);
            return await GetProductByIdAsync(code);
        }

        // Admin update — no ownership check
        public async Task<ProductDto> AdminUpdateProductAsync(string code, ProductUpdateDto dto)
        {
            // FIX: FindAsync uses PK (int Id). Use FirstOrDefaultAsync to query by Code string.
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Code == code);

            if (product == null)
                throw new NotFoundException("Product", "Code", code);

            await ApplyProductUpdate(product, dto);
            return await GetProductByIdAsync(code);
        }

        // Shared update logic used by both vendor and admin
        private async Task ApplyProductUpdate(Product product, ProductUpdateDto dto)
        {
            var category = await _context.Categories
                .Include(c => c.SubCategories)
                .FirstOrDefaultAsync(c => c.Id == dto.CategoryId && c.Status == CategoryStatus.Active);

            if (category == null)
                throw new NotFoundException("Category", dto.CategoryId);

            if (category.SubCategories.Any())
                throw new BadRequestException("Products must be added to a subcategory, not a parent category");

            if (dto.DiscountPrice.HasValue && dto.DiscountPrice >= dto.Price)
                throw new BadRequestException("Discount price must be less than regular price");

            product.NameAr = dto.NameAr;
            product.NameEn = dto.NameEn;
            product.DescriptionAr = dto.DescriptionAr;
            product.DescriptionEn = dto.DescriptionEn;
            product.Price = dto.Price;
            product.DiscountPrice = dto.DiscountPrice;
            product.StockQuantity = dto.StockQuantity;
            product.CategoryId = dto.CategoryId;
            product.MadeInCity = dto.MadeInCity;
            product.MadeInGovernorate = dto.MadeInGovernorate;
            product.IsFeatured = dto.IsFeatured;
            product.IsActive = dto.IsActive;
            product.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        // FIX: Interface signature is (string code, string vendorCode).
        // Soft-delete by code with vendor ownership check via vendor's Code.
        public async Task DeleteProductAsync(string code, string vendorCode)
        {
            var product = await _context.Products
                .Include(p => p.Vendor)
                .FirstOrDefaultAsync(p => p.Code == code);

            if (product == null)
                throw new NotFoundException("Product", "Code", code);

            if (product.Vendor.Code != vendorCode)
                throw new UnauthorizedException("You can only delete your own products");

            product.IsActive = false;
            product.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        public async Task AdminDeleteProductAsync(string code)
        {
            // FIX: FindAsync uses PK (int Id). Use FirstOrDefaultAsync to query by Code string.
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Code == code);

            if (product == null)
                throw new NotFoundException("Product", "Code", code);

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> UpdateStockAsync(int id, int quantity, int? vendorId = null)
        {
            var product = await _context.Products
                .Include(p => p.Vendor)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
                throw new NotFoundException("Product", id);

            if (vendorId.HasValue && product.VendorId != vendorId.Value)
                throw new UnauthorizedException("You can only update stock for your own products");

            if (quantity < 0)
                throw new BadRequestException("Stock quantity cannot be negative");

            product.StockQuantity = quantity;
            product.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task AdminUpdateStockAsync(string code, int quantity)
        {
            var product = await _context.Products.FindAsync(code);
            if (product == null)
                throw new NotFoundException("Product", "Code",  code);

            if (quantity < 0)
                throw new BadRequestException("Stock quantity cannot be negative");

            product.StockQuantity = quantity;
            product.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        public async Task<ProductDto> AdminToggleProductStatusAsync(string code)
        {
            // FIX: FindAsync uses PK (int Id). Use FirstOrDefaultAsync to query by Code string.
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Code == code);

            if (product == null)
                throw new NotFoundException("Product", "Code", code);

            product.IsActive = !product.IsActive;
            product.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return await GetProductByIdAsync(code);
        }

        public async Task IncrementViewCountAsync(int id)
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Products SET ViewCount = ViewCount + 1 WHERE Id = {id}");
        }

        private ProductDto MapToDto(Product product)
        {
            var finalPrice = product.DiscountPrice ?? product.Price;
            var discountPercentage = product.DiscountPrice.HasValue
                ? (int)Math.Round(((product.Price - product.DiscountPrice.Value) / product.Price) * 100)
                : 0;
            var avgRating = product.Reviews.Any() ? product.Reviews.Average(r => r.Rating) : 0;

            return new ProductDto
            {
                Id = product.Id,
                NameAr = product.NameAr,
                NameEn = product.NameEn,
                DescriptionAr = product.DescriptionAr,
                DescriptionEn = product.DescriptionEn,
                Price = product.Price,
                DiscountPrice = product.DiscountPrice,
                FinalPrice = finalPrice,
                DiscountPercentage = discountPercentage,
                StockQuantity = product.StockQuantity,
                SKU = product.SKU,
                IsEgyptianMade = product.IsEgyptianMade,
                MadeInCity = product.MadeInCity,
                MadeInGovernorate = product.MadeInGovernorate?.ToString(),
                IsFeatured = product.IsFeatured,
                IsActive = product.IsActive,
                InStock = product.StockQuantity > 0,
                ViewCount = product.ViewCount,
                AverageRating = Math.Round(avgRating, 1),
                TotalReviews = product.Reviews.Count,
                CreatedAt = product.CreatedAt,
                UpdatedAt = product.UpdatedAt,
                VendorId = product.VendorId,
                VendorName = product.Vendor.StoreName,
                VendorNameAr = product.Vendor.StoreNameAr,
                CategoryId = product.CategoryId,
                CategoryNameAr = product.Category.NameAr,
                CategoryNameEn = product.Category.NameEn,
                Code = product.Code,
                Images = product.Images
                    .Select(i => new ProductImageDto
                    {
                        Id = i.Id,
                        ImageUrl = i.ImageUrl,
                        IsPrimary = i.IsPrimary,
                        DisplayOrder = i.DisplayOrder
                    }).ToList(),
                Variants = product.Variants
                    .GroupBy(v => v.TypeName)
                    .OrderBy(g => g.Key)
                    .Select(g => new ProductVariantGroupDto
                    {
                        TypeName = g.Key,
                        Options = g.OrderBy(v => v.DisplayOrder)
                            .ThenBy(v => v.Value)
                            .Select(v => new ProductVariantDto
                            {
                                Id = v.Id,
                                TypeName = v.TypeName,
                                Value = v.Value,
                                ColorHex = v.ColorHex,
                                PriceAdjustment = v.PriceAdjustment,
                                StockQuantity = v.StockQuantity,
                                DisplayOrder = v.DisplayOrder,
                                IsActive = v.IsActive
                            })
                            .ToList()
                    })
                    .ToList()
            };
        }

        private ProductListDto MapToListDto(Product product)
        {
            var finalPrice = product.DiscountPrice ?? product.Price;
            var discountPercentage = product.DiscountPrice.HasValue
                ? (int)Math.Round(((product.Price - product.DiscountPrice.Value) / product.Price) * 100)
                : 0;
            var avgRating = product.Reviews.Any() ? product.Reviews.Average(r => r.Rating) : 0;
            var primaryImage = product.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
                               ?? product.Images.FirstOrDefault()?.ImageUrl;

            return new ProductListDto
            {
                Id = product.Id,
                NameAr = product.NameAr,
                NameEn = product.NameEn,
                Price = product.Price,
                DiscountPrice = product.DiscountPrice,
                FinalPrice = finalPrice,
                DiscountPercentage = discountPercentage,
                InStock = product.StockQuantity > 0,
                IsFeatured = product.IsFeatured,
                IsActive = product.IsActive,
                PrimaryImageUrl = primaryImage,
                AverageRating = Math.Round(avgRating, 1),
                TotalReviews = product.Reviews.Count,
                VendorName = product.Vendor.StoreName,
                CategoryNameEn = product.Category.NameEn,
                CategoryNameAr = product.Category.NameAr,
                Code = product.Code,
                CreatedAt = product.CreatedAt,
                UpdatedAt = product.UpdatedAt
            };
        }
    }
}
