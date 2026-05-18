namespace Graduation.DAL.Entities
{
    public class ActivityLog
    {
        public int Id { get; set; }
        public string AdminId { get; set; } = string.Empty;
        public AppUser? Admin { get; set; }
        public string Action { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string? EntityIdentifier { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
