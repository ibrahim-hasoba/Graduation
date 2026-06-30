using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Entities;
using Graduation.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Graduation.BLL.DTOs.ReturnRequest;
using Graduation.BLL.Errors;

namespace Graduation.BLL.Services.Implementations
{
    public class ReturnRequestService : IReturnRequestService
    {
        private readonly IUnitOfWork _uow;

        public ReturnRequestService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<ReturnRequestDto> CreateAsync(string userId, CreateReturnRequestDto dto)
        {
            var order = await _uow.Repository<Order>().GetByIdAsync(dto.OrderId)
                ?? throw new NotFoundException("Order", dto.OrderId);

            if (order.UserId != userId)
                throw new BadRequestException("Order does not belong to this user");

            if (order.Status != OrderStatus.Delivered)
                throw new BadRequestException("Only delivered orders can be returned");

            var existing = await _uow.Repository<ReturnRequest>().Query()
                .AnyAsync(r => r.OrderId == dto.OrderId && r.Status == ReturnRequestStatus.Pending);
            if (existing)
                throw new BadRequestException("A return request for this order is already pending");

            var request = new ReturnRequest
            {
                OrderId = dto.OrderId,
                UserId = userId,
                Reason = dto.Reason,
                Status = ReturnRequestStatus.Pending,
                CreatedAt = DateTime.UtcNow,
            };

            _uow.Repository<ReturnRequest>().Add(request);
            await _uow.SaveChangesAsync();
            return await GetByIdInternalAsync(request.Id);
        }

        public async Task<ReturnRequestDto> UpdateStatusAsync(int returnId, string reviewerId, UpdateReturnStatusDto dto)
        {
            var request = await _uow.Repository<ReturnRequest>().Query()
                .Include(r => r.Order)
                .FirstOrDefaultAsync(r => r.Id == returnId)
                ?? throw new NotFoundException("Return request", returnId);

            if (request.Status != ReturnRequestStatus.Pending)
                throw new BadRequestException("Return request has already been reviewed");

            request.Status = dto.Status;
            request.ReviewedById = reviewerId;
            request.ReviewedAt = DateTime.UtcNow;

            if (dto.Status == ReturnRequestStatus.Rejected)
            {
                if (string.IsNullOrWhiteSpace(dto.RejectionReason))
                    throw new BadRequestException("Rejection reason is required");
                request.RejectionReason = dto.RejectionReason;
            }

            if (dto.Status == ReturnRequestStatus.Approved)
            {
                request.Order.Status = OrderStatus.Returned;
                request.Order.ReturnedAt = DateTime.UtcNow;
                if (request.Order.PaymentStatus == PaymentStatus.Paid)
                    request.Order.PaymentStatus = PaymentStatus.Refunded;
            }

            await _uow.SaveChangesAsync();
            return await GetByIdInternalAsync(returnId);
        }

        public async Task<List<ReturnRequestDto>> GetByOrderAsync(int orderId)
        {
            return await _uow.Repository<ReturnRequest>().Query()
                .Include(r => r.User)
                .Include(r => r.ReviewedBy)
                .Include(r => r.Order)
                .Where(r => r.OrderId == orderId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => MapToDto(r))
                .ToListAsync();
        }

        public async Task<List<ReturnRequestDto>> GetByUserAsync(string userId)
        {
            return await _uow.Repository<ReturnRequest>().Query()
                .Include(r => r.ReviewedBy)
                .Include(r => r.Order)
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => MapToDto(r))
                .ToListAsync();
        }

        public async Task<List<ReturnRequestDto>> GetAllAsync()
        {
            return await _uow.Repository<ReturnRequest>().Query()
                .Include(r => r.User)
                .Include(r => r.ReviewedBy)
                .Include(r => r.Order)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => MapToDto(r))
                .ToListAsync();
        }

        private async Task<ReturnRequestDto> GetByIdInternalAsync(int id)
        {
            var r = await _uow.Repository<ReturnRequest>().Query()
                .Include(x => x.User)
                .Include(x => x.ReviewedBy)
                .Include(x => x.Order)
                .FirstAsync(x => x.Id == id);
            return MapToDto(r);
        }

        private static ReturnRequestDto MapToDto(ReturnRequest r) => new()
        {
            Id = r.Id,
            OrderId = r.OrderId,
            OrderNumber = r.Order.OrderNumber,
            UserId = r.UserId,
            UserName = $"{r.User.FirstName} {r.User.LastName}",
            Reason = r.Reason,
            Status = r.Status.ToString(),
            CreatedAt = r.CreatedAt,
            ReviewedAt = r.ReviewedAt,
            ReviewedByName = r.ReviewedBy != null ? $"{r.ReviewedBy.FirstName} {r.ReviewedBy.LastName}" : null,
            RejectionReason = r.RejectionReason,
        };
    }
}
