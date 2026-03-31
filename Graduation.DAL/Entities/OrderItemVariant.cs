using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Graduation.DAL.Entities
{
    public class OrderItemVariant
    {
        public int OrderItemId { get; set; }
        public OrderItem OrderItem { get; set; } = null!;

        public int ProductVariantId { get; set; }
        public ProductVariant ProductVariant { get; set; } = null!;
    }
}
