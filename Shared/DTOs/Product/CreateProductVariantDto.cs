using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DTOs.Product
{
    public class CreateProductVariantDto
    {
        public string TypeName { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string? ColorHex { get; set; }
        public decimal PriceAdjustment { get; set; } = 0;
        public int? StockQuantity { get; set; }
        public int DisplayOrder { get; set; } = 0;
    }
}
