using Graduation.DAL.Entities;

namespace Shared.DTOs.Vendor
{
    public class VendorDto
    {
        public int Id { get; set; }

        public string? Code { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string UserFullName { get; set; } = string.Empty;
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
        public bool IsApproved { get; set; }
        public bool IsActive { get; set; }
        public string ApprovalStatus { get; set; } = "Pending";
        public int ApprovalStatusId { get; set; }
        public string? RejectionReason { get; set; }
        public int TotalProducts { get; set; }
        public int TotalOrders { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}