namespace Shared.DTOs.Category
{
    public class CategoryHierarchyDto
    {
        public int Id { get; set; }
        public string NameAr { get; set; } = string.Empty;
        public string NameEn { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public int? ParentCategoryId { get; set; }
        public int ProductCount { get; set; }
        public List<CategoryHierarchyDto> SubCategories { get; set; } = new();
    }
}
