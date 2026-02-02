using System;
using System.Collections.Generic;
using System.Text;

namespace Shared.DTOs.Order
{
    public class OrderListDto
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public int StatusId { get; set; }
        public DateTime OrderDate { get; set; }
        public int ItemsCount { get; set; }
        public string VendorName { get; set; } = string.Empty;
    }
}
