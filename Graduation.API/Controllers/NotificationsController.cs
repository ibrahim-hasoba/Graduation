using Graduation.API.Errors;
using Graduation.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    /// Get user's notifications
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetNotifications([FromQuery] bool unreadOnly = false)
    {
      var userId = User.FindFirst("userId")?.Value;
      if (string.IsNullOrEmpty(userId))
        return Unauthorized(new { success = false, message = "User not authenticated" });

      try
      {
        var notifications = await _notificationService.GetUserNotificationsAsync(userId, unreadOnly);
        return Ok(new { success = true, count = notifications.Count, data = notifications });
      }
      catch (Exception ex)
      {
        return BadRequest(new { success = false, message = ex.Message });
      }
    }

    /// <summary>
    /// Get unread notification count
    /// </summary>
    [HttpGet("unread/count")]
    public async Task<IActionResult> GetUnreadCount()
    {
      var userId = User.FindFirst("userId")?.Value;
      if (string.IsNullOrEmpty(userId))
        return Unauthorized(new { success = false, message = "User not authenticated" });

      try
      {
        var count = await _notificationService.GetUnreadCountAsync(userId);
        return Ok(new { success = true, unreadCount = count });
      }
      catch (Exception ex)
      {
        return BadRequest(new { success = false, message = ex.Message });
      }
    }

    /// <summary>
    /// Mark notification as read
    /// </summary>
    [HttpPatch("{notificationId}/read")]
    public async Task<IActionResult> MarkAsRead(int notificationId)
    {
      var userId = User.FindFirst("userId")?.Value;
      if (string.IsNullOrEmpty(userId))
        return Unauthorized(new { success = false, message = "User not authenticated" });

      try
      {
        await _notificationService.MarkAsReadAsync(userId, notificationId);
        return Ok(new { success = true, message = "Notification marked as read" });
      }
      catch (NotFoundException ex)
      {
        return NotFound(new { success = false, message = ex.Message });
      }
      catch (Exception ex)
      {
        return BadRequest(new { success = false, message = ex.Message });
      }
    }

    /// <summary>
    /// Mark all notifications as read
    /// </summary>
    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
      var userId = User.FindFirst("userId")?.Value;
      if (string.IsNullOrEmpty(userId))
        return Unauthorized(new { success = false, message = "User not authenticated" });

      try
      {
        await _notificationService.MarkAllAsReadAsync(userId);
        return Ok(new { success = true, message = "All notifications marked as read" });
      }
      catch (Exception ex)
      {
        return BadRequest(new { success = false, message = ex.Message });
      }
    }

    /// <summary>
    /// Delete notification
    /// </summary>
    [HttpDelete("{notificationId}")]
    public async Task<IActionResult> DeleteNotification(int notificationId)
    {
      var userId = User.FindFirst("userId")?.Value;
      if (string.IsNullOrEmpty(userId))
        return Unauthorized(new { success = false, message = "User not authenticated" });

      try
      {
        await _notificationService.DeleteNotificationAsync(userId, notificationId);
        return Ok(new { success = true, message = "Notification deleted" });
      }
      catch (NotFoundException ex)
      {
        return NotFound(new { success = false, message = ex.Message });
      }
      catch (Exception ex)
      {
        return BadRequest(new { success = false, message = ex.Message });
      }
    }
  }
}
