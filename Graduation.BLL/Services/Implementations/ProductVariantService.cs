using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.DTOs.Product;
using Shared.Errors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Graduation.BLL.Services.Implementations
{
    public class ProductVariantService : IProductVariantService
    {
        private readonly DatabaseContext _context;
        private readonly ILogger<ProductVariantService> _logger;

        public ProductVariantService(DatabaseContext context, ILogger<ProductVariantService> logger)
        {
            _context = context;
            _logger = logger;
        }


        private async Task<Product> GetProductAndVerifyOwnerAsync(int productId, int vendorId)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (product == null)
                throw new NotFoundException("Product", productId);

            if (product.VendorId != vendorId)
                throw new UnauthorizedException("You can only manage variants for your own products.");

            return product;
        }

        private static ProductVariantDto MapToDto(ProductVariant v) => new()
        {
            Id = v.Id,
            TypeName = v.TypeName,
            Value = v.Value,
            ColorHex = v.ColorHex,
            PriceAdjustment = v.PriceAdjustment,
            StockQuantity = v.StockQuantity,
            DisplayOrder = v.DisplayOrder,
            IsActive = v.IsActive
        };

        private static ProductVariantGroupDto BuildGroup(
            string typeName, IEnumerable<ProductVariant> variants) => new()
            {
                TypeName = typeName,
                Options = variants
                .OrderBy(v => v.DisplayOrder)
                .ThenBy(v => v.Value)
                .Select(MapToDto)
                .ToList()
            };


        public async Task<List<ProductVariantGroupDto>> GetProductVariantsAsync(int productId)
        {
            var exists = await _context.Products.AnyAsync(p => p.Id == productId && p.IsActive);
            if (!exists)
                throw new NotFoundException("Product", productId);

            var variants = await _context.ProductVariants
                .Where(v => v.ProductId == productId && v.IsActive)
                .OrderBy(v => v.TypeName)
                .ThenBy(v => v.DisplayOrder)
                .ThenBy(v => v.Value)
                .ToListAsync();

            return variants
                .GroupBy(v => v.TypeName)
                .Select(g => BuildGroup(g.Key, g))
                .ToList();
        }

        public async Task<ProductVariantDto> GetVariantByIdAsync(int variantId)
        {
            var variant = await _context.ProductVariants
                .FirstOrDefaultAsync(v => v.Id == variantId && v.IsActive);

            if (variant == null)
                throw new NotFoundException("Variant", variantId);

            return MapToDto(variant);
        }


        public async Task<ProductVariantDto> AddVariantAsync(
            int productId, int vendorId, CreateProductVariantDto dto)
        {
            await GetProductAndVerifyOwnerAsync(productId, vendorId);

            var duplicate = await _context.ProductVariants
                .AnyAsync(v => v.ProductId == productId
                            && v.TypeName == dto.TypeName
                            && v.Value == dto.Value
                            && v.IsActive);

            if (duplicate)
                throw new ConflictException(
                    $"A variant with type '{dto.TypeName}' and value '{dto.Value}' already exists for this product.");

            var variant = new ProductVariant
            {
                ProductId = productId,
                TypeName = NormalizeTypeName(dto.TypeName),
                Value = dto.Value.Trim(),
                ColorHex = dto.ColorHex?.Trim(),
                PriceAdjustment = dto.PriceAdjustment,
                StockQuantity = dto.StockQuantity,
                DisplayOrder = dto.DisplayOrder,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.ProductVariants.Add(variant);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Variant added: ProductId={ProductId}, Type={Type}, Value={Value}",
                productId, variant.TypeName, variant.Value);

            return MapToDto(variant);
        }

        public async Task<ProductVariantGroupDto> BulkUpsertVariantTypeAsync(
            int productId, int vendorId, BulkUpsertVariantTypeDto dto)
        {
            await GetProductAndVerifyOwnerAsync(productId, vendorId);

            var normalizedType = NormalizeTypeName(dto.TypeName);

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var existing = await _context.ProductVariants
                    .Where(v => v.ProductId == productId
                             && v.TypeName == normalizedType
                             && v.IsActive)
                    .ToListAsync();

                foreach (var v in existing)
                    v.IsActive = false;

                var newVariants = dto.Options.Select((opt, idx) => new ProductVariant
                {
                    ProductId = productId,
                    TypeName = normalizedType,
                    Value = opt.Value.Trim(),
                    ColorHex = opt.ColorHex?.Trim(),
                    PriceAdjustment = opt.PriceAdjustment,
                    StockQuantity = opt.StockQuantity,
                    DisplayOrder = opt.DisplayOrder == 0 ? idx : opt.DisplayOrder,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                }).ToList();

                _context.ProductVariants.AddRange(newVariants);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation(
                    "Bulk upsert variants: ProductId={ProductId}, Type={Type}, Count={Count}",
                    productId, normalizedType, newVariants.Count);

                return BuildGroup(normalizedType, newVariants);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<ProductVariantDto> UpdateVariantAsync(
            int variantId, int vendorId, UpdateProductVariantDto dto)
        {
            var variant = await _context.ProductVariants
                .Include(v => v.Product)
                .FirstOrDefaultAsync(v => v.Id == variantId && v.IsActive);

            if (variant == null)
                throw new NotFoundException("Variant", variantId);

            if (variant.Product.VendorId != vendorId)
                throw new UnauthorizedException("You can only update variants for your own products.");

            var isDuplicate = await _context.ProductVariants
                .AnyAsync(v => v.Id != variantId
                            && v.ProductId == variant.ProductId
                            && v.TypeName == dto.TypeName
                            && v.Value == dto.Value
                            && v.IsActive);

            if (isDuplicate)
                throw new ConflictException(
                    $"A variant with type '{dto.TypeName}' and value '{dto.Value}' already exists for this product.");

            variant.TypeName = NormalizeTypeName(dto.TypeName);
            variant.Value = dto.Value.Trim();
            variant.ColorHex = dto.ColorHex?.Trim();
            variant.PriceAdjustment = dto.PriceAdjustment;
            variant.StockQuantity = dto.StockQuantity;
            variant.DisplayOrder = dto.DisplayOrder;
            variant.IsActive = dto.IsActive;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Variant updated: VariantId={VariantId}, Type={Type}, Value={Value}",
                variantId, variant.TypeName, variant.Value);

            return MapToDto(variant);
        }

        public async Task DeleteVariantAsync(int variantId, int vendorId)
        {
            var variant = await _context.ProductVariants
                .Include(v => v.Product)
                .FirstOrDefaultAsync(v => v.Id == variantId && v.IsActive);

            if (variant == null)
                throw new NotFoundException("Variant", variantId);

            if (variant.Product.VendorId != vendorId)
                throw new UnauthorizedException("You can only delete variants for your own products.");

            variant.IsActive = false;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Variant soft-deleted: VariantId={VariantId}", variantId);
        }

        public async Task DeleteVariantTypeAsync(int productId, int vendorId, string typeName)
        {
            await GetProductAndVerifyOwnerAsync(productId, vendorId);

            var normalized = NormalizeTypeName(typeName);

            var variants = await _context.ProductVariants
                .Where(v => v.ProductId == productId && v.TypeName == normalized && v.IsActive)
                .ToListAsync();

            if (!variants.Any())
                throw new NotFoundException($"No active variants of type '{typeName}' found for this product.");

            foreach (var v in variants)
                v.IsActive = false;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Variant type deleted: ProductId={ProductId}, Type={Type}, Count={Count}",
                productId, normalized, variants.Count);
        }


        private static string NormalizeTypeName(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName)) return typeName;
            var t = typeName.Trim();
            return char.ToUpperInvariant(t[0]) + t[1..].ToLowerInvariant();
        }
    }
}
