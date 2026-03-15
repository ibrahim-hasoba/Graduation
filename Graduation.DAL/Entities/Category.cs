namespace Graduation.DAL.Entities
{
    public class Category
    {
        public int Id { get; set; }

        public string? Code { get; set; }
        public string NameAr { get; set; } = string.Empty;
        public string NameEn { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public int? ParentCategoryId { get; set; }
        public Category? ParentCategory { get; set; }
        public bool IsActive => Status == CategoryStatus.Active;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public CategoryStatus Status { get; set; } = CategoryStatus.Active;
        public ICollection<Category> SubCategories { get; set; } = new List<Category>();
        public ICollection<Product> Products { get; set; } = new List<Product>();
    }

    public enum CategoryStatus
    {
        Active = 1,
        Inactive = 2
    }
}