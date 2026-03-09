using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DTOs.Product
{
    public class BulkUpsertVariantTypeDto
    {
        public string TypeName { get; set; } = string.Empty;


        public List<CreateProductVariantDto> Options { get; set; } = new();
    }
}
