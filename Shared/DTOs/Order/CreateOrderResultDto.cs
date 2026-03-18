using Shared.DTOs.Payment;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DTOs.Order
{
    public class CreateOrderResultDto
    {
        public List<OrderDto> Orders { get; set; } = new();
        public int TotalOrdersCreated { get; set; }

        public PaymentDto? Payment { get; set; }
    }
}
