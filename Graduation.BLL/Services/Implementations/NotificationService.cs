using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.DTOs.Notification;
using Shared.Errors;

namespace Graduation.BLL.Services.Implementations
{
    public class NotificationService : INotificationService
    {
        private readonly DatabaseContext _context;
        private readonly IFirebaseService _firebaseService;
        private readonly ILogger<NotificationService> _logger;
        public NotificationService(DatabaseContext context , IFirebaseService firebaseService , ILogger<NotificationService> logger)
        {
            _context = context;
            _firebaseService = firebaseService;
            _logger = logger;
        }

        
        public async Task<PagedNotificationResultDto> GetUserNotificationsAsync(
            string userId,
            bool unreadOnly = false,
            int pageNumber = 1,
            int pageSize = 20)
        {
            var query = _context.Notifications
                .Where(n => n.UserId == userId)
                .AsQueryable();

            if (unreadOnly)
                query = query.Where(n => !n.IsRead);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(n => n.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(n => new NotificationDto
                {
                    Id = n.Id,
                    Title = n.Title,
                    Message = n.Message,
                    Type = n.Type,
                    OrderId = n.OrderId,
                    ProductId = n.ProductId,
                    VendorId = n.VendorId,
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt,
                    ReadAt = n.ReadAt
                })
                .ToListAsync();

            return new PagedNotificationResultDto
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                UnreadCount = unreadOnly
                    ? items.Count(n => !n.IsRead)
                    : await _context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead)
            };
        }

        public async Task BulkDeleteAsync(IEnumerable<int> ids, string userId)
        {
            await _context.Notifications
                .Where(n => ids.Contains(n.Id) && n.UserId == userId)
                .ExecuteDeleteAsync();
        }

        public async Task<int> GetUnreadCountAsync(string userId)
            => await _context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);

        public async Task MarkAsReadAsync(int notificationId, string userId)
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
            }
        }

        public async Task MarkAllAsReadAsync(string userId)
        {
            await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(n => n.IsRead, true)
                    .SetProperty(n => n.ReadAt, DateTime.UtcNow));
        }

        public async Task DeleteNotificationAsync(int notificationId, string userId)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification == null)
                throw new NotFoundException("Notification not found");

            _context.Notifications.Remove(notification);
            await _context.SaveChangesAsync();
        }

        public async Task CreateNotificationAsync(
       string userId,
       string title,
       string message,
       string type = "",
       int? orderId = null,
       int? productId = null,
       int? vendorId = null)
        {
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

            try
            {
                var user = await _context.Users
                    .Where(u => u.Id == userId)
                    .Select(u => new { u.FcmToken })
                    .FirstOrDefaultAsync();

                if (user != null && !string.IsNullOrEmpty(user.FcmToken))
                {
                    var customData = new Dictionary<string, string>
                {
                    { "type", type },
                    { "orderId", orderId?.ToString() ?? "" },
                    { "productId", productId?.ToString() ?? "" }
                };

                    await _firebaseService.SendPushNotificationAsync(
                        user.FcmToken, title, message, customData);
                }
                else
                {
                    _logger.LogDebug(
                        "No FCM token found for user {UserId}, skipping push notification", userId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to send push notification to user {UserId} with title '{Title}'",
                    userId, title);
            }
        }

        public async Task CreateNotificationForVendorAsync(
            int vendorId,
            string title,
            string message,
            string type = "",
            int? orderId = null,
            int? productId = null)
        {
            var vendor = await _context.Vendors.FindAsync(vendorId);
            if (vendor == null) return;

            await CreateNotificationAsync(vendor.UserId, title, message, type,
                orderId: orderId, productId: productId, vendorId: vendorId);
        }
    }
}
