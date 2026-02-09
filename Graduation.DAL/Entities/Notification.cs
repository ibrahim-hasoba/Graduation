namespace Graduation.DAL.Entities
{
  public class Notification
  {
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public AppUser? User { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "OrderStatus", "Review", "Product", "Vendor", "SystemAlert"
    public int? OrderId { get; set; }
    public int? ProductId { get; set; }
    public int? VendorId { get; set; }
    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
  }
}
