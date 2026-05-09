namespace Shared.DTOs.Review
{
    public class CreateReviewReportDto
    {
        public int ReviewId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
