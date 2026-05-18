using System.ComponentModel.DataAnnotations;

namespace Shared.DTOs.ReturnRequest
{
    public class CreateReturnRequestDto
    {
        [Required] public int OrderId { get; set; }
        [Required] [MaxLength(1000)] public string Reason { get; set; } = string.Empty;
    }
}
