namespace Shared.DTOs.Notification
{
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
