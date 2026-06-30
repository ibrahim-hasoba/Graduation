using System.ComponentModel.DataAnnotations;

namespace Graduation.BLL.DTOs.ReturnRequest
{
    public class CreateReturnRequestDto
    {
        [Required] public int OrderId { get; set; }
        [Required] [MaxLength(1000)] public string Reason { get; set; } = string.Empty;
    }
}
