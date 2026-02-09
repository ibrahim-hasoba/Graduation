namespace Shared.DTOs.Category
{
  public class UpdateCategoryDto
  {
    public string? NameAr { get; set; }
    public string? NameEn { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int? ParentCategoryId { get; set; }
  }
}
