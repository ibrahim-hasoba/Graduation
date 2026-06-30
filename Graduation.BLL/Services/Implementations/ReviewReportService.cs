using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Graduation.BLL.DTOs;
using Graduation.BLL.DTOs.Review;
using Graduation.BLL.Errors;

namespace Graduation.BLL.Services.Implementations
{
    public class ReviewReportService : IReviewReportService
    {
        private readonly DatabaseContext _context;
        private readonly IReviewService _reviewService;

        public ReviewReportService(DatabaseContext context, IReviewService reviewService)
        {
            _context = context;
            _reviewService = reviewService;
        }

        public async Task<ReviewReportDto> CreateReportAsync(string userId, CreateReviewReportDto dto)
        {
            var review = await _context.ProductReviews
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.Id == dto.ReviewId)
                ?? throw new NotFoundException("Review", dto.ReviewId);

            if (review.UserId == userId)
                throw new BadRequestException("You cannot report your own review");

            var existing = await _context.Set<ReviewReport>()
                .AnyAsync(r => r.ReviewId == dto.ReviewId && r.ReportedByUserId == userId && r.Status == ReviewReportStatus.Pending);

            if (existing)
                throw new ConflictException("You have already reported this review");

            var report = new ReviewReport
            {
                ReviewId = dto.ReviewId,
                ReportedByUserId = userId,
                Reason = dto.Reason,
                Status = ReviewReportStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _context.Set<ReviewReport>().Add(report);
            await _context.SaveChangesAsync();

            return await GetReportByIdAsync(report.Id)
                ?? throw new Exception("Failed to retrieve created report");
        }

        public async Task<PagedResult<ReviewReportDto>> GetPendingReportsAsync(int pageNumber, int pageSize)
        {
            var query = _context.Set<ReviewReport>()
                .Include(r => r.Review)
                .Include(r => r.ReportedByUser)
                .Include(r => r.ResolvedBy)
                .Where(r => r.Status == ReviewReportStatus.Pending)
                .OrderByDescending(r => r.CreatedAt);

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<ReviewReportDto>
            {
                Items = items.Select(MapToDto).ToList(),
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<ReviewReportDto?> GetReportByIdAsync(int reportId)
        {
            var report = await _context.Set<ReviewReport>()
                .Include(r => r.Review)
                .Include(r => r.ReportedByUser)
                .Include(r => r.ResolvedBy)
                .FirstOrDefaultAsync(r => r.Id == reportId);

            return report == null ? null : MapToDto(report);
        }

        public async Task<bool> ApproveReportAsync(int reportId, string adminUserId)
        {
            var report = await _context.Set<ReviewReport>()
                .Include(r => r.Review)
                .FirstOrDefaultAsync(r => r.Id == reportId);

            if (report == null) return false;
            if (report.Status != ReviewReportStatus.Pending) return false;

            report.Status = ReviewReportStatus.Approved;
            report.ResolvedAt = DateTime.UtcNow;
            report.ResolvedById = adminUserId;

            var review = report.Review;
            if (review != null)
            {
                review.IsApproved = false;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DismissReportAsync(int reportId, string adminUserId)
        {
            var report = await _context.Set<ReviewReport>()
                .FirstOrDefaultAsync(r => r.Id == reportId);

            if (report == null) return false;
            if (report.Status != ReviewReportStatus.Pending) return false;

            report.Status = ReviewReportStatus.Dismissed;
            report.ResolvedAt = DateTime.UtcNow;
            report.ResolvedById = adminUserId;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteReviewFromReportAsync(int reportId, string adminUserId)
        {
            var report = await _context.Set<ReviewReport>()
                .Include(r => r.Review)
                .FirstOrDefaultAsync(r => r.Id == reportId);

            if (report == null) return false;

            var review = report.Review;
            if (review != null)
            {
                _context.ProductReviews.Remove(review);
            }

            report.Status = ReviewReportStatus.Approved;
            report.ResolvedAt = DateTime.UtcNow;
            report.ResolvedById = adminUserId;

            await _context.SaveChangesAsync();
            return true;
        }

        private static ReviewReportDto MapToDto(ReviewReport report) => new()
        {
            Id = report.Id,
            ReviewId = report.ReviewId,
            Reason = report.Reason,
            Status = report.Status,
            CreatedAt = report.CreatedAt,
            ResolvedAt = report.ResolvedAt,
            ReportedByUserId = report.ReportedByUserId,
            ReportedByUserName = $"{report.ReportedByUser?.FirstName} {report.ReportedByUser?.LastName}".Trim(),
            ResolvedByUserId = report.ResolvedById,
            ResolvedByName = report.ResolvedBy != null
                ? $"{report.ResolvedBy.FirstName} {report.ResolvedBy.LastName}".Trim()
                : null
        };
    }
}
