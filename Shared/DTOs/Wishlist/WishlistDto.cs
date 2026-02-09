namespace Shared.DTOs.Wishlist
{
  public class WishlistDto
  {
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductNameAr { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }
    public decimal FinalPrice { get; set; }
    public string? ImageUrl { get; set; }
    public string VendorName { get; set; } = string.Empty;
    public int VendorId { get; set; }
    public bool InStock { get; set; }
    public double AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public DateTime AddedAt { get; set; }
  }

  public class AddToWishlistDto
  {
    public int ProductId { get; set; }
  }
}
