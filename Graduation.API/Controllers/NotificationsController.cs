using Graduation.API.Errors;
using Graduation.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Graduation.API.Extensions;
using Shared.DTOs.Notification;
using System.Security.Claims;

namespace Graduation.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notificationService;

        public NotificationsController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        /// <summary>
        /// Get user's notifications with optional pagination and unread filter.
        /// FIXED: Added pageNumber/pageSize query parameters to prevent unbounded result sets.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetNotifications(
          [FromQuery] bool unreadOnly = false,
          [FromQuery] int pageNumber = 1,
          [FromQuery] int pageSize = 20)
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var notifications = await _notificationService.GetUserNotificationsAsync(userId, unreadOnly);

            // Apply pagination in-memory (service returns all; pagination keeps API contract stable
            // without requiring a service-layer breaking change)
            var totalCount = notifications.Count;
            var paged = notifications
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Ok(new Errors.ApiResult(
                data: new
                {
                    items = paged,
                    totalCount,
                    pageNumber,
                    pageSize,
                    totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                },
                count: totalCount));
        }

        /// <summary>
        /// Get unread notification count
        /// </summary>
        [HttpGet("unread/count")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            var count = await _notificationService.GetUnreadCountAsync(userId);
            return Ok(new ApiResult(data: new { unreadCount = count }));
        }

        /// <summary>
        /// Mark notification as read
        /// </summary>
        [HttpPatch("{notificationId}/read")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> MarkAsRead(int notificationId)
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            try
            {
                await _notificationService.MarkAsReadAsync(userId, notificationId);
                return Ok(new ApiResult(message: "Notification marked as read"));
            }
            catch (NotFoundException ex)
            {
                return NotFound(new ApiResponse(404, ex.Message));
            }
        }

        /// <summary>
        /// Mark all notifications as read
        /// </summary>
        [HttpPatch("read-all")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            await _notificationService.MarkAllAsReadAsync(userId);
            return Ok(new ApiResult(message: "All notifications marked as read"));
        }

        /// <summary>
        /// Delete notification
        /// </summary>
        [HttpDelete("{notificationId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DeleteNotification(int notificationId)
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            try
            {
                await _notificationService.DeleteNotificationAsync(userId, notificationId);
                return Ok(new ApiResult(message: "Notification deleted"));
            }
            catch (NotFoundException ex)
            {
                return NotFound(new ApiResponse(404, ex.Message));
            }
        }

        [HttpDelete("bulk")]
        [ProducesResponseType( StatusCodes.Status200OK)]
        [ProducesResponseType( StatusCodes.Status401Unauthorized)]

        public async Task<IActionResult> BulkDelete([FromBody] BulkDeleteNotificationsDto dto)
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var deletedCount = await _notificationService.BulkDeleteAsync(userId, dto.Ids);
            return Ok(new ApiResult ( "Notifications deleted permanently" ));
        }
    }
}
