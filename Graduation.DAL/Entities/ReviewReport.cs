namespace Graduation.DAL.Entities
{
    public enum ReviewReportStatus
    {
        Pending = 0,
        Approved = 1,
        Dismissed = 2
    }

    public class ReviewReport
    {
        public int Id { get; set; }
        public string Reason { get; set; } = string.Empty;
        public ReviewReportStatus Status { get; set; } = ReviewReportStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ResolvedAt { get; set; }
        public string? ResolvedById { get; set; }

        public int ReviewId { get; set; }
        public ProductReview Review { get; set; } = null!;

        public string ReportedByUserId { get; set; } = string.Empty;
        public AppUser ReportedByUser { get; set; } = null!;

        public AppUser? ResolvedBy { get; set; }
    }
}
