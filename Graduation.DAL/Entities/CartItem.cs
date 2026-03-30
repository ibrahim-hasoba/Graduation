namespace Graduation.DAL.Entities
{
    public class CartItem
    {
        public int Id { get; set; }
        public int Quantity { get; set; }
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        public int? VariantId { get; set; }
        public virtual ProductVariant? Variant { get; set; }

        public string UserId { get; set; } = string.Empty;
        public AppUser User { get; set; } = null!;
        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;
    }
}