using System;
using System.Collections.Generic;
using System.Text;

namespace Shared.DTOs.Cart
{
    public class CartDto
    {
        public List<CartItemDto> Items { get; set; } = new();
        public int TotalItems { get; set; }
        public decimal SubTotal { get; set; }
        public decimal ShippingCost { get; set; }
        public decimal TotalAmount { get; set; }
        public bool HasOutOfStockItems { get; set; }
    }
}
