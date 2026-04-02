namespace Shared.DTOs.Vendor
{
    public class PublicVendorDetailsDto
    {
        public int Id { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public string StoreNameAr { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string LogoUrl { get; set; } = string.Empty;
        public string BannerImageUrl { get; set; } = string.Empty;
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public DateTime JoinedDate { get; set; }
    }
}
