using System;
namespace Graduation.DAL.Entities
{
  public class EmailOtp
  {
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Purpose { get; set; } = "email_verification";
    public bool Consumed { get; set; } = false;
  }
}
