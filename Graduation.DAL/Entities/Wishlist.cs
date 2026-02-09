namespace Graduation.DAL.Entities
{
  public class Wishlist
  {
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public AppUser? User { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
  }
}
