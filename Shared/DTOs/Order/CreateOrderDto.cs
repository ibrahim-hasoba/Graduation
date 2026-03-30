using Graduation.DAL.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Shared.DTOs.Order
{
    public class CreateOrderDto
    {
        [Required(ErrorMessage = "First name is required")]
        public string ShippingFirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required")]
        public string ShippingLastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required")]
        [Phone]
        public string ShippingPhone { get; set; } = string.Empty;
        public int? AddressId { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? ShippingAddress { get; set; }


        [Required(ErrorMessage = "Payment method is required")]
        public PaymentMethod PaymentMethod { get; set; }
        public string ClientType { get; set; } = "web";
        public string? Notes { get; set; }
    }
}
