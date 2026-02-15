using System.ComponentModel.DataAnnotations;
using Graduation.DAL.Entities;

namespace Shared.DTOs.Vendor
{
    public class VendorUpdateDto
    {
        [Required(ErrorMessage = "Store name is required")]
        [MaxLength(200)]
        public string StoreName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Store name in Arabic is required")]
        [MaxLength(200)]
        public string StoreNameAr { get; set; } = string.Empty;

        [Required(ErrorMessage = "Store description is required")]
        public string StoreDescription { get; set; } = string.Empty;

        [Required(ErrorMessage = "Store description in Arabic is required")]
        public string StoreDescriptionAr { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required")]
        [Phone]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Address is required")]
        public string Address { get; set; } = string.Empty;

        [Required(ErrorMessage = "City is required")]
        public string City { get; set; } = string.Empty;

        [Required(ErrorMessage = "Governorate is required")]
        public EgyptianGovernorate Governorate { get; set; }

        public string? LogoUrl { get; set; }
        public string? BannerUrl { get; set; }
    }
}