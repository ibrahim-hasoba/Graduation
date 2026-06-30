using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Entities;
using Graduation.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Graduation.BLL.DTOs.Notification;
using Graduation.BLL.Errors;

namespace Graduation.BLL.Services.Implementations
{
    public class NotificationService : INotificationService
    {
        private readonly IUnitOfWork _uow;
        private readonly IFirebaseService _firebaseService;
        private readonly ILogger<NotificationService> _logger;
        public NotificationService(IUnitOfWork uow , IFirebaseService firebaseService , ILogger<NotificationService> logger)
        {
            _uow = uow;
            _firebaseService = firebaseService;
            _logger = logger;
        }

        public async Task<PagedNotificationResultDto> GetUserNotificationsAsync(
            string userId,
            bool unreadOnly = false,
            int pageNumber = 1,
            int pageSize = 20)
        {
            var query = _uow.Repository<Notification>().Query()
                .Where(n => n.UserId == userId);

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
                UnreadCount = await _uow.Repository<Notification>().Query()
                    .CountAsync(n => n.UserId == userId && !n.IsRead)
            };
        }

        public async Task BulkDeleteAsync(IEnumerable<int> ids, string userId)
        {
            var notifications = await _uow.Repository<Notification>().Query()
                .Where(n => ids.Contains(n.Id) && n.UserId == userId)
                .ToListAsync();

            _uow.Repository<Notification>().DeleteRange(notifications);
            await _uow.SaveChangesAsync();
        }

        public async Task<int> GetUnreadCountAsync(string userId)
            => await _uow.Repository<Notification>().Query()
                .CountAsync(n => n.UserId == userId && !n.IsRead);

        public async Task MarkAsReadAsync(int notificationId, string userId)
        {
            var notification = await _uow.Repository<Notification>().Query()
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification == null)
                throw new NotFoundException("Notification not found");

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
                await _uow.SaveChangesAsync();
            }
        }

        public async Task MarkAllAsReadAsync(string userId)
        {
            await _uow.Context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(n => n.IsRead, true)
                    .SetProperty(n => n.ReadAt, DateTime.UtcNow));
        }

        public async Task DeleteNotificationAsync(int notificationId, string userId)
        {
            var notification = await _uow.Repository<Notification>().Query()
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification == null)
                throw new NotFoundException("Notification not found");

            _uow.Repository<Notification>().Delete(notification);
            await _uow.SaveChangesAsync();
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

            _uow.Repository<Notification>().Add(notification);
            await _uow.SaveChangesAsync();

            try
            {
                var user = await _uow.Repository<AppUser>().Query()
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

                    var result = await _firebaseService.SendPushNotificationAsync(
                        user.FcmToken, title, message, customData);

                    if (result == FcmSendResult.InvalidToken)
                    {
                        await _uow.Context.Users
                            .Where(u => u.Id == userId)
                            .ExecuteUpdateAsync(s => s.SetProperty(u => u.FcmToken, (string?)null));
                        _logger.LogInformation("Cleared invalid FCM token for user {UserId}", userId);
                    }
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
            var vendor = await _uow.Repository<Vendor>().GetByIdAsync(vendorId);
            if (vendor == null) return;

            await CreateNotificationAsync(vendor.UserId, title, message, type,
                orderId: orderId, productId: productId, vendorId: vendorId);
        }
    }
}
