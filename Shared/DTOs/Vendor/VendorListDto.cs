namespace Shared.DTOs.Vendor
{
    public class VendorListDto
    {
        public int Id { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public string StoreNameAr { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public string City { get; set; } = string.Empty;
        public string Governorate { get; set; } = string.Empty;
        public bool IsApproved { get; set; }
        public bool IsActive { get; set; }
        public int TotalProducts { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}