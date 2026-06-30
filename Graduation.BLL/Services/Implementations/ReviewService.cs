using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Entities;
using Graduation.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Graduation.BLL.DTOs;
using Graduation.BLL.DTOs.Review;
using Graduation.BLL.Errors;

namespace Graduation.BLL.Services.Implementations
{
    public class ReviewService : IReviewService
    {
        private readonly IUnitOfWork _uow;
        private readonly INotificationService _notificationService;

        public ReviewService(IUnitOfWork uow, INotificationService notificationService)
        {
            _uow = uow;
            _notificationService = notificationService;
        }

        public async Task<ReviewDto> CreateReviewAsync(string userId, CreateReviewDto dto)
        {
            var product = await _uow.Repository<Product>().Query()
                .IgnoreQueryFilters()
                .Include(p => p.Vendor)
                .FirstOrDefaultAsync(p => p.Id == dto.ProductId)
                ?? throw new NotFoundException("Product", dto.ProductId);

            var hasPurchased = await _uow.Repository<OrderItem>().Query()
                .IgnoreQueryFilters()
                .AnyAsync(oi => oi.ProductId == dto.ProductId
                             && oi.Order.UserId == userId
                             && oi.Order.Status == OrderStatus.Delivered);

            if (!hasPurchased)
                throw new BadRequestException(
                    "You can only review products you have purchased and received.");

            var existing = await _uow.Repository<ProductReview>().Query()
                .FirstOrDefaultAsync(r => r.ProductId == dto.ProductId && r.UserId == userId);

            if (existing != null)
                throw new ConflictException("You have already reviewed this product.");

            var review = new ProductReview
            {
                ProductId = dto.ProductId,
                UserId = userId,
                Rating = dto.Rating,
                Comment = dto.Comment,
                IsApproved = false,
                CreatedAt = DateTime.UtcNow
            };

            _uow.Repository<ProductReview>().Add(review);
            await _uow.SaveChangesAsync();

            if (product.VendorId > 0)
            {
                await _notificationService.CreateNotificationForVendorAsync(
                    vendorId: product.VendorId,
                    title: "New Review Received",
                    message: $"Your product \"{product.NameEn}\" received a {dto.Rating}-star review and is pending approval.",
                    type: "Review",
                    productId: product.Id);
            }

            return await GetReviewByIdAsync(review.Id)
                ?? throw new Exception("Failed to retrieve created review");
        }

        public async Task<ReviewDto?> GetReviewByIdAsync(int reviewId)
        {
            var review = await _uow.Repository<ProductReview>().Query()
                .IgnoreQueryFilters()
                .Include(r => r.User)
                .Include(r => r.Product)
                .FirstOrDefaultAsync(r => r.Id == reviewId);

            return review == null ? null : MapToDto(review);
        }

        public async Task<PagedResult<ReviewDto>> GetProductReviewsAsync(
                        int productId, int pageNumber, int pageSize, bool approvedOnly = true)
        {
            var query = _uow.Repository<ProductReview>().Query()
                .Include(r => r.User)
                .Include(r => r.Product)
                .Where(r => r.ProductId == productId);

            if (approvedOnly)
                query = query.Where(r => r.IsApproved);

            query = query.OrderByDescending(r => r.CreatedAt);

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<ReviewDto>
            {
                Items = items.Select(MapToDto).ToList(),
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<PagedResult<ReviewDto>> GetPendingReviewsAsync(int pageNumber, int pageSize)
        {
            var query = _uow.Repository<ProductReview>().Query()
                .IgnoreQueryFilters()
                .Include(r => r.User)
                .Include(r => r.Product)
                .Where(r => !r.IsApproved)
                .OrderByDescending(r => r.CreatedAt);

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<ReviewDto>
            {
                Items = items.Select(MapToDto).ToList(),
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<List<ReviewDto>> GetPendingReviewsAsync(int vendorId)
        {
            var reviews = await _uow.Repository<ProductReview>().Query()
                .IgnoreQueryFilters()
                .Include(r => r.User)
                .Include(r => r.Product)
                .Where(r => r.Product.VendorId == vendorId && !r.IsApproved)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return reviews.Select(MapToDto).ToList();
        }

        public async Task<PagedResult<ReviewDto>> GetUserReviewsAsync(
                    string userId, int pageNumber, int pageSize)
        {
            var query = _uow.Repository<ProductReview>().Query()
                .IgnoreQueryFilters()
                .Include(r => r.User)
                .Include(r => r.Product)
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt);

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<ReviewDto>
            {
                Items = items.Select(MapToDto).ToList(),
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<bool> ApproveReviewAsync(int id, bool isApproved = true)
        {
            var review = await _uow.Repository<ProductReview>().Query()
                .IgnoreQueryFilters()
                .Include(r => r.Product)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (review == null) return false;

            review.IsApproved = isApproved;
            await _uow.SaveChangesAsync();

            if (!string.IsNullOrEmpty(review.UserId))
            {
                var statusText = isApproved ? "Approved" : "Rejected";
                await _notificationService.CreateNotificationAsync(
                    userId: review.UserId,
                    title: $"Your Review Was {statusText}",
                    message: isApproved
                        ? $"Your review for \"{review.Product?.NameEn}\" has been approved and is now visible."
                        : $"Your review for \"{review.Product?.NameEn}\" was not approved.",
                    type: "Review",
                    productId: review.ProductId);
            }

            return true;
        }

        public async Task<bool> DeleteReviewAsync(int id, string userId, bool isAdmin = false)
        {
            var review = await _uow.Repository<ProductReview>().Query()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.Id == id);

            if (review == null) return false;

            if (!isAdmin && review.UserId != userId) return false;

            _uow.Repository<ProductReview>().Delete(review);
            await _uow.SaveChangesAsync();
            return true;
        }

        public async Task<ReviewSummaryDto> GetProductReviewSummaryAsync(int productId)
        {
            var reviews = await _uow.Repository<ProductReview>().Query()
                .Where(r => r.ProductId == productId && r.IsApproved)
                .ToListAsync();

            if (!reviews.Any())
                return new ReviewSummaryDto { ProductId = productId, TotalReviews = 0 };

            var distribution = Enumerable.Range(1, 5)
                .ToDictionary(star => star, star => reviews.Count(r => r.Rating == star));

            return new ReviewSummaryDto
            {
                ProductId = productId,
                AverageRating = Math.Round(reviews.Average(r => r.Rating), 1),
                TotalReviews = reviews.Count,
                RatingDistribution = distribution
            };
        }

        private ReviewDto MapToDto(ProductReview review) => new()
        {
            Id = review.Id,
            ProductId = review.ProductId,
            ProductName = review.Product?.NameEn ?? string.Empty,
            UserId = review.UserId,
            UserName = $"{review.User?.FirstName} {review.User?.LastName}".Trim(),
            Rating = review.Rating,
            Comment = review.Comment,
            IsApproved = review.IsApproved,
            CreatedAt = review.CreatedAt
        };
    }
}
