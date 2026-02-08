using Graduation.DAL.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Shared.DTOs.Product
{
    public class ProductCreateDto
    {
        [Required(ErrorMessage = "Product name in Arabic is required")]
        [MinLength(3, ErrorMessage = "Product name must be at least 3 characters")]
        [MaxLength(300)]
        public string NameAr { get; set; } = string.Empty;

        [Required(ErrorMessage = "Product name in English is required")]
        [MinLength(3, ErrorMessage = "Product name must be at least 3 characters")]
        [MaxLength(300)]
        public string NameEn { get; set; } = string.Empty;

        [Required(ErrorMessage = "Product description in Arabic is required")]
        [MinLength(10, ErrorMessage = "Description must be at least 10 characters")]
        public string DescriptionAr { get; set; } = string.Empty;

        [Required(ErrorMessage = "Product description in English is required")]
        [MinLength(10, ErrorMessage = "Description must be at least 10 characters")]
        public string DescriptionEn { get; set; } = string.Empty;

        [Required(ErrorMessage = "Price is required")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
        public decimal Price { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Discount price cannot be negative")]
        public decimal? DiscountPrice { get; set; }

        [Required(ErrorMessage = "Stock quantity is required")]
        [Range(0, int.MaxValue, ErrorMessage = "Stock quantity cannot be negative")]
        public int StockQuantity { get; set; }

        [Required(ErrorMessage = "SKU is required")]
        [MinLength(3, ErrorMessage = "SKU must be at least 3 characters")]
        [MaxLength(100)]
        [RegularExpression(@"^[A-Za-z0-9-_]+$", ErrorMessage = "SKU can only contain letters, numbers, hyphens, and underscores")]
        public string SKU { get; set; } = string.Empty;

        [Required(ErrorMessage = "Category is required")]
        [Range(1, int.MaxValue, ErrorMessage = "Category ID must be valid")]
        public int CategoryId { get; set; }

        public bool IsEgyptianMade { get; set; } = true;

        [MinLength(2, ErrorMessage = "City name must be at least 2 characters")]
        [MaxLength(100)]
        public string? MadeInCity { get; set; }

        public EgyptianGovernorate? MadeInGovernorate { get; set; }

        public bool IsFeatured { get; set; } = false;

        public List<string>? ImageUrls { get; set; }
    }
}
