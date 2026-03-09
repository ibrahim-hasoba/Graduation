
using Shared.DTOs.Notification;

namespace Graduation.BLL.Services.Interfaces
{
    public interface INotificationService
    {

        Task<PagedNotificationResultDto> GetUserNotificationsAsync(
            string userId,
            bool unreadOnly = false,
            int pageNumber = 1,
            int pageSize = 20);

        Task<int> GetUnreadCountAsync(string userId);
        Task MarkAsReadAsync(int notificationId, string userId);
        Task MarkAllAsReadAsync(string userId);
        Task DeleteNotificationAsync(int notificationId, string userId);
        Task CreateNotificationAsync(
            string userId,
            string title,
            string message,
            string type = "",
            int? orderId = null,
            int? productId = null,
            int? vendorId = null);

        Task CreateNotificationForVendorAsync(
            int vendorId,
            string title,
            string message,
            string type = "",
            int? orderId = null,
            int? productId = null);
        Task BulkDeleteAsync(IEnumerable<int> ids, string userId);
    }
}
