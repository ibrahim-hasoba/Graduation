using System;
using System.Collections.Generic;
using System.Text;

namespace Shared.DTOs.Product
{
    public class ProductSearchDto
    {
        public string? SearchTerm { get; set; }
        public int? CategoryId { get; set; }
        public int? VendorId { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public bool? IsEgyptianMade { get; set; }
        public int? GovernorateId { get; set; }
        public bool? InStock { get; set; }
        public bool? IsFeatured { get; set; }
        public string? SortBy { get; set; } // price_asc, price_desc, rating, newest, popular
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
