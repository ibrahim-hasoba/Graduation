using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Graduation.API.Controllers
{
    [Route("api/admin/return-requests")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminReturnRequestsController : BaseController
    {
        private readonly DatabaseContext _context;
        private readonly IActivityLogService _activityLog;

        public AdminReturnRequestsController(
            DatabaseContext context,
            IActivityLogService activityLog,
            ILanguageService lang)
            : base(lang)
        {
            _context = context;
            _activityLog = activityLog;
        }

        /// <summary>Gets all return requests (admin overview).</summary>
        [HttpGet]
        public async Task<IActionResult> GetAllReturnRequests()
        {
            var requests = await _context.ReturnRequests
                .Include(r => r.User)
                .Include(r => r.ReviewedBy)
                .Include(r => r.Order)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new Graduation.BLL.DTOs.ReturnRequest.ReturnRequestDto
                {
                    Id = r.Id,
                    OrderId = r.OrderId,
                    OrderNumber = r.Order.OrderNumber,
                    UserId = r.UserId,
                    UserName = r.User.FirstName + " " + r.User.LastName,
                    Reason = r.Reason,
                    Status = r.Status.ToString(),
                    CreatedAt = r.CreatedAt,
                    ReviewedAt = r.ReviewedAt,
                    ReviewedByName = r.ReviewedBy != null ? r.ReviewedBy.FirstName + " " + r.ReviewedBy.LastName : null,
                    RejectionReason = r.RejectionReason,
                })
                .ToListAsync();
            return OkResult(data: requests);
        }

        /// <summary>Approves or rejects a return request.</summary>
        [HttpPost("{returnId}/review")]
        public async Task<IActionResult> ReviewReturnRequest(int returnId, [FromBody] Graduation.BLL.DTOs.ReturnRequest.UpdateReturnStatusDto dto)
        {
            var adminId = GetRequiredUserId();
            var result = await _context.ReturnRequests
                .Include(r => r.Order)
                .FirstOrDefaultAsync(r => r.Id == returnId)
                ?? throw new Graduation.BLL.Errors.NotFoundException("Return request", returnId);

            if (result.Status != DAL.Entities.ReturnRequestStatus.Pending)
                throw new Graduation.BLL.Errors.BadRequestException("Return request has already been reviewed");

            result.Status = dto.Status;
            result.ReviewedById = adminId;
            result.ReviewedAt = DateTime.UtcNow;

            if (dto.Status == DAL.Entities.ReturnRequestStatus.Rejected)
            {
                if (string.IsNullOrWhiteSpace(dto.RejectionReason))
                    throw new Graduation.BLL.Errors.BadRequestException("Rejection reason is required");
                result.RejectionReason = dto.RejectionReason;
            }

            if (dto.Status == DAL.Entities.ReturnRequestStatus.Approved)
            {
                result.Order.Status = DAL.Entities.OrderStatus.Returned;
                result.Order.ReturnedAt = DateTime.UtcNow;
                if (result.Order.PaymentStatus == DAL.Entities.PaymentStatus.Paid)
                    result.Order.PaymentStatus = DAL.Entities.PaymentStatus.Refunded;
            }

            await _context.SaveChangesAsync();
            await _activityLog.LogAsync(adminId, dto.Status.ToString(), "Return", returnId.ToString(), $"{dto.Status} return request #{returnId}");
            return OkResult(message: $"Return request {dto.Status}");
        }
    }
}
