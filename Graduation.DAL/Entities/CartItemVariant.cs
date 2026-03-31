using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Graduation.DAL.Entities
{
    public class CartItemVariant
    {
        public int CartItemId { get; set; }
        public CartItem CartItem { get; set; }

        public int ProductVariantId { get; set; }
        public ProductVariant ProductVariant { get; set; }
    }
}
