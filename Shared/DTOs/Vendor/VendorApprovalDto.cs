namespace Graduation.API.DTOs.Vendor
{
    public class VendorApprovalDto
    {
        public bool IsApproved { get; set; }
        public string? RejectionReason { get; set; }
    }
}