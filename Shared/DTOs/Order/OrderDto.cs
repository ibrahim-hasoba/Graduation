using System;
using System.Collections.Generic;
using System.Text;

namespace Shared.DTOs.Order
{
    public class OrderDto
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public decimal SubTotal { get; set; }
        public decimal ShippingCost { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public int StatusId { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public DateTime? DeliveredAt { get; set; }

        // Shipping Info
        public string ShippingFirstName { get; set; } = string.Empty;
        public string ShippingLastName { get; set; } = string.Empty;
        public string ShippingAddress { get; set; } = string.Empty;
        public string ShippingCity { get; set; } = string.Empty;
        public string ShippingGovernorate { get; set; } = string.Empty;
        public string ShippingPhone { get; set; } = string.Empty;
        public string? Notes { get; set; }

        // Vendor Info
        public int VendorId { get; set; }
        public string VendorName { get; set; } = string.Empty;

        // Items
        public List<OrderItemDto> Items { get; set; } = new();
    }
}
