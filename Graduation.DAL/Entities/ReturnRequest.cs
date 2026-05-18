namespace Graduation.DAL.Entities
{
    public enum ReturnRequestStatus
    {
        Pending = 1,
        Approved = 2,
        Rejected = 3
    }

    public class ReturnRequest
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public Order Order { get; set; } = null!;
        public string UserId { get; set; } = string.Empty;
        public AppUser User { get; set; } = null!;
        public string Reason { get; set; } = string.Empty;
        public ReturnRequestStatus Status { get; set; } = ReturnRequestStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewedById { get; set; }
        public AppUser? ReviewedBy { get; set; }
        public string? RejectionReason { get; set; }
    }
}
