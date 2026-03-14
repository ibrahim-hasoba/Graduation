using Graduation.DAL.Entities;

namespace Shared.DTOs.Vendor
{
    public class AdminUpdateVendorDto
    {
        public string? StoreName { get; set; }
        public string? StoreNameAr { get; set; }
        public string? StoreDescription { get; set; }
        public string? StoreDescriptionAr { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public EgyptianGovernorate? Governorate { get; set; }
        public string? LogoUrl { get; set; }
        public string? BannerUrl { get; set; }
        public bool? IsApproved { get; set; }
        public bool? IsActive { get; set; }
    }
}
