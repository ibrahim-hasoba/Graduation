using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DTOs.Cart
{
    public class CartItemVariantDto
    {
        public int VariantId { get; set; }
        public string TypeName { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string? ColorHex { get; set; }
        public decimal PriceAdjustment { get; set; }
    }
}
