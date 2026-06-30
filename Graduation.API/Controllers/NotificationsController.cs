using Graduation.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Graduation.API.Extensions;
using Graduation.BLL.DTOs.Notification;

namespace Graduation.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class NotificationsController : BaseController
    {
        private readonly INotificationService _notificationService;

        public NotificationsController(INotificationService notificationService, ILanguageService lang)
            : base(lang)
        {
            _notificationService = notificationService;
        }
        /// <summary>Gets a paginated list of notifications for the authenticated user.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet]
        public async Task<IActionResult> GetNotifications(
            [FromQuery] bool unreadOnly = false,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var userId = GetRequiredUserId();
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var result = await _notificationService.GetUserNotificationsAsync(
                userId, unreadOnly, pageNumber, pageSize);

            return OkResult(data: result, count: result.TotalCount);
        }
        /// <summary>Gets the count of unread notifications for the authenticated user.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet("unread/count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = GetRequiredUserId();
            var count = await _notificationService.GetUnreadCountAsync(userId);
            return OkResult(data: new { unreadCount = count });
        }
        /// <summary>Marks a specific notification as read.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPatch("{notificationId}/read")]
        public async Task<IActionResult> MarkAsRead(int notificationId)
        {
            var userId = GetRequiredUserId();
            await _notificationService.MarkAsReadAsync(notificationId, userId);
            return OkResult(message: Lang.GetMessage(LangKeys.Notification.MarkedRead));
        }
        /// <summary>Marks all notifications as read for the authenticated user.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpPatch("read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = GetRequiredUserId();
            await _notificationService.MarkAllAsReadAsync(userId);
            return OkResult(message: Lang.GetMessage(LangKeys.Notification.AllMarkedRead));
        }
        /// <summary>Deletes a specific notification by ID.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpDelete("{notificationId}")]
        public async Task<IActionResult> DeleteNotification(int notificationId)
        {
            var userId = GetRequiredUserId();
            await _notificationService.DeleteNotificationAsync(notificationId, userId);
            return OkResult(message: Lang.GetMessage(LangKeys.Notification.Deleted));
        }
        /// <summary>Deletes multiple notifications by their IDs.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpDelete("bulk")]
        public async Task<IActionResult> BulkDelete([FromBody] BulkDeleteNotificationsDto dto)
        {
            var userId = GetRequiredUserId();
            await _notificationService.BulkDeleteAsync(dto.Ids, userId);
            return OkResult(message: Lang.GetMessage(LangKeys.Notification.BulkDeleted));
        }
    }
}
