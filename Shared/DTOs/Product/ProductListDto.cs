using System;
using System.Collections.Generic;
using System.Text;

namespace Shared.DTOs.Product
{
    public class ProductListDto
    {
        public int Id { get; set; }

        public string? Code { get; set; }
        public string NameAr { get; set; } = string.Empty;
        public string NameEn { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal? DiscountPrice { get; set; }
        public decimal FinalPrice { get; set; }
        public int DiscountPercentage { get; set; }
        public bool InStock { get; set; }
        public bool IsFeatured { get; set; }
        public bool IsActive { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? PrimaryImageUrl { get; set; }
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public string VendorName { get; set; } = string.Empty;
        public string CategoryNameEn { get; set; } = string.Empty;
        public string CategoryNameAr { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
