namespace Shared.DTOs.Category
{
    public class CategoryDto
    {
        public string? Code { get; set; }
        public int Id { get; set; }
        public string NameAr { get; set; } = string.Empty;
        public string NameEn { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public string? ParentCategoryCode { get; set; }
        public int? ParentCategoryId { get; set; }
        public int ProductCount { get; set; }
        public string Status { get; set; } = "Active";
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public List<CategoryDto> SubCategories { get; set; } = new();
    }
}
