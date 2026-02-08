namespace Shared.DTOs.Category
{
    /// <summary>
    /// Represents a category with its full path from root to leaf
    /// Example: Electronics -> Mobile -> iPhone
    /// </summary>
    public class CategoryPathDto
    {
        public int Id { get; set; }
        public string NameAr { get; set; } = string.Empty;
        public string NameEn { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public int Level { get; set; } // 0 = root, 1 = child, 2 = grandchild, etc.
        
        /// <summary>
        /// Full path as "Electronics -> Mobile -> iPhone"
        /// </summary>
        public string PathAr { get; set; } = string.Empty;
        public string PathEn { get; set; } = string.Empty;
        
        /// <summary>
        /// List of category IDs from root to this category
        /// For accessing parent categories
        /// </summary>
        public List<int> CategoryChain { get; set; } = new();
        
        /// <summary>
        /// Parent category info (one level up)
        /// </summary>
        public CategoryDto? ParentCategory { get; set; }
        
        /// <summary>
        /// All subcategories under this category
        /// </summary>
        public List<CategoryPathDto> SubCategories { get; set; } = new();
        
        public int ProductCount { get; set; }
    }
}
