using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DTOs.Product
{
    public class ProductVariantGroupDto
    {
        public string TypeName { get; set; } = string.Empty;
        public List<ProductVariantDto> Options { get; set; } = new();
    }
}
