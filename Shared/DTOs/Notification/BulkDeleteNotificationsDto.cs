namespace Shared.DTOs.Notification
{
    public class BulkDeleteNotificationsDto
    {
        /// <summary>Notification IDs to delete. Empty = delete all.</summary>
        public List<int> Ids { get; set; } = new();
    }
}