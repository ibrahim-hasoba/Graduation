using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DTOs.Order
{
    public class OrderMapTrackingDto
    {
        public string OrderNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;

        public double? DeliveryLatitude { get; set; }
        public double? DeliveryLongitude { get; set; }

        public double? VendorLatitude { get; set; }
        public double? VendorLongitude { get; set; }

        public double? CurrentLatitude { get; set; }
        public double? CurrentLongitude { get; set; }
    }
}
