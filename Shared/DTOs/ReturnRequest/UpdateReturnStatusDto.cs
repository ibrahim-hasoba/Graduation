using System.ComponentModel.DataAnnotations;
using Graduation.DAL.Entities;

namespace Shared.DTOs.ReturnRequest
{
    public class UpdateReturnStatusDto
    {
        [Required] public ReturnRequestStatus Status { get; set; }
        public string? RejectionReason { get; set; }
    }
}
