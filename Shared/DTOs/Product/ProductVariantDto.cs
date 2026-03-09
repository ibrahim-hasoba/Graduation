using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DTOs.Product
{
    public class ProductVariantDto
    {
        public int Id { get; set; }
        public string TypeName { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string? ColorHex { get; set; }
        public decimal PriceAdjustment { get; set; }
        public int? StockQuantity { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
    }
}
