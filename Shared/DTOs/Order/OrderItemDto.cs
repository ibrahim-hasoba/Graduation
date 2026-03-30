using System;
using System.Collections.Generic;
using System.Text;

namespace Shared.DTOs.Order
{
    public class OrderItemDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ProductNameAr { get; set; } = string.Empty;
        public string ProductNameEn { get; set; } = string.Empty;
        public string? ProductImage { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public int? VariantId { get; set; }
        public string? VariantTypeName { get; set; }
        public string? VariantValue { get; set; }
        public string? VariantColorHex { get; set; }
    }
}
