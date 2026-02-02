using System;
using System.Collections.Generic;
using System.Text;

namespace Shared.DTOs.Cart
{
    public class CartItemDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ProductNameAr { get; set; } = string.Empty;
        public string ProductNameEn { get; set; } = string.Empty;
        public string? ProductImage { get; set; }
        public decimal Price { get; set; }
        public decimal? DiscountPrice { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal TotalPrice { get; set; }
        public int StockAvailable { get; set; }
        public bool InStock { get; set; }
        public int VendorId { get; set; }
        public string VendorName { get; set; } = string.Empty;
        public DateTime AddedAt { get; set; }
    }
}
