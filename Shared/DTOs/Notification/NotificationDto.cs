namespace Shared.DTOs.Notification
{
  public class NotificationDto
  {
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int? OrderId { get; set; }
    public int? ProductId { get; set; }
    public int? VendorId { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
  }

  public class MarkNotificationReadDto
  {
    public int NotificationId { get; set; }
  }

  public class SendNotificationDto
  {
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int? OrderId { get; set; }
    public int? ProductId { get; set; }
    public int? VendorId { get; set; }
  }
}
