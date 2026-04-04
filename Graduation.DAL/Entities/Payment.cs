using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Graduation.DAL.Entities
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PaymentStatus2
    {
        Pending = 1,  
        Paid = 2,   
        Failed = 3,   
        Refunded = 4 
    }

    public class Payment
    {
        public int Id { get; set; }
        public string? Code { get; set; }  
        public int OrderId { get; set; }
        public Order Order { get; set; } = null!;
        public PaymentMethod Method { get; set; } = PaymentMethod.CreditCard;
        public PaymentStatus2 Status { get; set; } = PaymentStatus2.Pending;
        public decimal Amount { get; set; }
        public string? PaymentUrl { get; set; }
        public string? PaymobTransactionId { get; set; }
        public bool? IsSuccess { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? PaidAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
