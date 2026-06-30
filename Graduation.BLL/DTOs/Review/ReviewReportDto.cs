using Graduation.DAL.Entities;

namespace Graduation.BLL.DTOs.Review
{
    public class ReviewReportDto
    {
        public int Id { get; set; }
        public int? ReviewId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public ReviewReportStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }

        public string ReportedByUserId { get; set; } = string.Empty;
        public string ReportedByUserName { get; set; } = string.Empty;

        public string? ResolvedByUserId { get; set; }
        public string? ResolvedByName { get; set; }
    }
}
