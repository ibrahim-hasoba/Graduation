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

        [Required(ErrorMessage = "Address is required")]
        public string ShippingAddress { get; set; } = string.Empty;

        [Required(ErrorMessage = "City is required")]
        public string ShippingCity { get; set; } = string.Empty;

        [Required(ErrorMessage = "Governorate is required")]
        public EgyptianGovernorate ShippingGovernorate { get; set; }

        [Required(ErrorMessage = "Phone number is required")]
        [Phone]
        public string ShippingPhone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Payment method is required")]
        public PaymentMethod PaymentMethod { get; set; }

        public string? Notes { get; set; }
    }
}
