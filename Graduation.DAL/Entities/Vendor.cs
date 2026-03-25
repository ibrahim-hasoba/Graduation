namespace Graduation.DAL.Entities
{
    public enum VendorApprovalStatus
    {
        Pending = 0,
        Approved = 1,
        Rejected = 2
    }


    public class Vendor
    {
        public int Id { get; set; }

        public string? Code { get; set; }
        public string UserId { get; set; } = string.Empty;
        public AppUser User { get; set; } = null!;
        public string StoreName { get; set; } = string.Empty;
        public string StoreNameAr { get; set; } = string.Empty;
        public string StoreDescription { get; set; } = string.Empty;
        public string StoreDescriptionAr { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public string? BannerUrl { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public bool IsApproved => ApprovalStatus == VendorApprovalStatus.Approved;
        public bool IsActive { get; set; } = true;
        public VendorApprovalStatus ApprovalStatus { get; set; } = VendorApprovalStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public string? RejectionReason { get; set; }
        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}