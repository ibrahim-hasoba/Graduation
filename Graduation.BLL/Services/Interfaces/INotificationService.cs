using Shared.DTOs.Notification;

namespace Graduation.BLL.Services.Interfaces
{
  public interface INotificationService
  {
    Task<NotificationDto> SendNotificationAsync(string userId, string title, string message, string type, int? orderId = null, int? productId = null, int? vendorId = null);
    Task<List<NotificationDto>> GetUserNotificationsAsync(string userId, bool unreadOnly = false);
    Task<int> GetUnreadCountAsync(string userId);
    Task MarkAsReadAsync(string userId, int notificationId);
    Task MarkAllAsReadAsync(string userId);
    Task DeleteNotificationAsync(string userId, int notificationId);
    Task ClearOldNotificationsAsync(int daysOld = 30);
  }
}
