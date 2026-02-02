using System;
using System.Collections.Generic;
using System.Text;

namespace Shared.DTOs.Product
{
    public class ProductDto
    {
        public int Id { get; set; }
        public string NameAr { get; set; } = string.Empty;
        public string NameEn { get; set; } = string.Empty;
        public string DescriptionAr { get; set; } = string.Empty;
        public string DescriptionEn { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal? DiscountPrice { get; set; }
        public decimal FinalPrice { get; set; }
        public int DiscountPercentage { get; set; }
        public int StockQuantity { get; set; }
        public string SKU { get; set; } = string.Empty;
        public bool IsEgyptianMade { get; set; }
        public string? MadeInCity { get; set; }
        public string? MadeInGovernorate { get; set; }
        public bool IsFeatured { get; set; }
        public bool IsActive { get; set; }
        public bool InStock { get; set; }
        public int ViewCount { get; set; }
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Vendor Info
        public int VendorId { get; set; }
        public string VendorName { get; set; } = string.Empty;
        public string VendorNameAr { get; set; } = string.Empty;

        // Category Info
        public int CategoryId { get; set; }
        public string CategoryNameAr { get; set; } = string.Empty;
        public string CategoryNameEn { get; set; } = string.Empty;

        // Images
        public List<ProductImageDto> Images { get; set; } = new();
    }
}
