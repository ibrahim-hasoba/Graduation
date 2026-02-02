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
        [MaxLength(300)]
        public string NameAr { get; set; } = string.Empty;

        [Required(ErrorMessage = "Product name in English is required")]
        [MaxLength(300)]
        public string NameEn { get; set; } = string.Empty;

        [Required(ErrorMessage = "Product description in Arabic is required")]
        public string DescriptionAr { get; set; } = string.Empty;

        [Required(ErrorMessage = "Product description in English is required")]
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
        [MaxLength(100)]
        public string SKU { get; set; } = string.Empty;

        [Required(ErrorMessage = "Category is required")]
        public int CategoryId { get; set; }

        public bool IsEgyptianMade { get; set; } = true;

        public string? MadeInCity { get; set; }

        public EgyptianGovernorate? MadeInGovernorate { get; set; }

        public bool IsFeatured { get; set; } = false;

        public List<string>? ImageUrls { get; set; }
    }
}
