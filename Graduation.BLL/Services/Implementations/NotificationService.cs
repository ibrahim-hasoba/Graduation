using Shared.Errors;
using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.DTOs.Notification;

namespace Graduation.BLL.Services.Implementations
{
  public class NotificationService : INotificationService
  {
    private readonly DatabaseContext _context;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        DatabaseContext context,
        ILogger<NotificationService> logger)
    {
      _context = context;
      _logger = logger;
    }

    public async Task<NotificationDto> SendNotificationAsync(
        string userId,
        string title,
        string message,
        string type,
        int? orderId = null,
        int? productId = null,
        int? vendorId = null)
    {
      var user = await _context.Users.FindAsync(userId);
      if (user == null)
        throw new NotFoundException("User not found");

      var notification = new Notification
      {
        UserId = userId,
        Title = title,
        Message = message,
        Type = type,
        OrderId = orderId,
        ProductId = productId,
        VendorId = vendorId,
        IsRead = false,
        CreatedAt = DateTime.UtcNow
      };

      _context.Notifications.Add(notification);
      await _context.SaveChangesAsync();

      _logger.LogInformation("Notification sent: Type={Type}, UserId={UserId}, NotificationId={NotificationId}",
          type, userId, notification.Id);

      return MapToNotificationDto(notification);
    }

    public async Task<List<NotificationDto>> GetUserNotificationsAsync(string userId, bool unreadOnly = false)
    {
      var query = _context.Notifications
          .Where(n => n.UserId == userId);

      if (unreadOnly)
        query = query.Where(n => !n.IsRead);

      var notifications = await query
          .OrderByDescending(n => n.CreatedAt)
          .ToListAsync();

      return notifications.Select(MapToNotificationDto).ToList();
    }

    public async Task<int> GetUnreadCountAsync(string userId)
    {
      return await _context.Notifications
          .CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    public async Task MarkAsReadAsync(string userId, int notificationId)
    {
      var notification = await _context.Notifications
          .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

      if (notification == null)
        throw new NotFoundException("Notification not found");

      if (!notification.IsRead)
      {
        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Notification marked as read: NotificationId={NotificationId}", notificationId);
      }
    }
    public async Task<int> BulkDeleteAsync(string userId, List<int> ids)
    {
        IQueryable<Notification> query = _context.Notifications
            .Where(n => n.UserId == userId);

        // Empty ids = delete ALL notifications for this user
        if (ids.Any())
            query = query.Where(n => ids.Contains(n.Id));

        return await query.ExecuteDeleteAsync();
    }

        public async Task MarkAllAsReadAsync(string userId)
    {
      var notifications = await _context.Notifications
          .Where(n => n.UserId == userId && !n.IsRead)
          .ToListAsync();

      foreach (var notification in notifications)
      {
        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;
      }

      if (notifications.Any())
      {
        await _context.SaveChangesAsync();
        _logger.LogInformation("All notifications marked as read: UserId={UserId}, Count={Count}",
            userId, notifications.Count);
      }
    }

    public async Task DeleteNotificationAsync(string userId, int notificationId)
    {
      var notification = await _context.Notifications
          .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

      if (notification == null)
        throw new NotFoundException("Notification not found");

      _context.Notifications.Remove(notification);
      await _context.SaveChangesAsync();

      _logger.LogInformation("Notification deleted: NotificationId={NotificationId}", notificationId);
    }

    public async Task ClearOldNotificationsAsync(int daysOld = 30)
    {
      var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);
      var oldNotifications = await _context.Notifications
          .Where(n => n.IsRead && n.CreatedAt < cutoffDate)
          .ToListAsync();

      if (oldNotifications.Any())
      {
        _context.Notifications.RemoveRange(oldNotifications);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Old notifications deleted: Count={Count}, OlderThan={Days} days",
            oldNotifications.Count, daysOld);
      }
    }

    private NotificationDto MapToNotificationDto(Notification notification)
    {
      return new NotificationDto
      {
        Id = notification.Id,
        Title = notification.Title,
        Message = notification.Message,
        Type = notification.Type,
        OrderId = notification.OrderId,
        ProductId = notification.ProductId,
        VendorId = notification.VendorId,
        IsRead = notification.IsRead,
        CreatedAt = notification.CreatedAt,
        ReadAt = notification.ReadAt
      };
    }
  }
}
