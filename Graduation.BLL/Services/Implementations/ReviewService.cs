using Graduation.API.Errors;
using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Shared.DTOs.Review;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Graduation.BLL.Services.Implementations
{
    public class ReviewService : IReviewService
    {
        private readonly DatabaseContext _context;

        public ReviewService(DatabaseContext context)
        {
            _context = context;
        }

        public async Task<ReviewDto> CreateReviewAsync(string userId, CreateReviewDto dto)
        {
            // Check if product exists
            var product = await _context.Products.FindAsync(dto.ProductId);
            if (product == null)
                throw new NotFoundException("Product", dto.ProductId);

            // Check if user already reviewed this product
            var existingReview = await _context.ProductReviews
                .FirstOrDefaultAsync(r => r.ProductId == dto.ProductId && r.UserId == userId);

            if (existingReview != null)
                throw new ConflictException("You have already reviewed this product");

            // Check if user has purchased this product
            var hasPurchased = await _context.OrderItems
                .Include(oi => oi.Order)
                .AnyAsync(oi => oi.ProductId == dto.ProductId
                    && oi.Order.UserId == userId
                    && oi.Order.Status == OrderStatus.Delivered);

            if (!hasPurchased)
                throw new BadRequestException("You can only review products you have purchased");

            var review = new ProductReview
            {
                ProductId = dto.ProductId,
                UserId = userId,
                Rating = dto.Rating,
                Comment = dto.Comment,
                CreatedAt = DateTime.UtcNow,
                IsApproved = false // Reviews need admin approval
            };

            _context.ProductReviews.Add(review);
            await _context.SaveChangesAsync();

            return await GetReviewByIdAsync(review.Id) ?? throw new NotFoundException("Review not found after creation");
        }

        public async Task<List<ReviewDto>> GetProductReviewsAsync(int productId, bool approvedOnly = true)
        {
            var query = _context.ProductReviews
                .Include(r => r.User)
                .Include(r => r.Product)
                .Where(r => r.ProductId == productId);

            if (approvedOnly)
                query = query.Where(r => r.IsApproved);

            var reviews = await query
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return reviews.Select(MapToDto).ToList();
        }

        public async Task<List<ReviewDto>> GetUserReviewsAsync(string userId)
        {
            var reviews = await _context.ProductReviews
                .Include(r => r.User)
                .Include(r => r.Product)
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return reviews.Select(MapToDto).ToList();
        }

        public async Task<bool> DeleteReviewAsync(int reviewId, string userId)
        {
            var review = await _context.ProductReviews
                .FirstOrDefaultAsync(r => r.Id == reviewId && r.UserId == userId);

            if (review == null)
                return false;

            _context.ProductReviews.Remove(review);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ApproveReviewAsync(int reviewId)
        {
            var review = await _context.ProductReviews.FindAsync(reviewId);
            if (review == null)
                return false;

            review.IsApproved = true;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<ReviewDto?> GetReviewByIdAsync(int reviewId)
        {
            var review = await _context.ProductReviews
                .Include(r => r.User)
                .Include(r => r.Product)
                .FirstOrDefaultAsync(r => r.Id == reviewId);

            return review != null ? MapToDto(review) : null;
        }

        private ReviewDto MapToDto(ProductReview review)
        {
            return new ReviewDto
            {
                Id = review.Id,
                ProductId = review.ProductId,
                ProductName = review.Product.NameEn,
                Rating = review.Rating,
                Comment = review.Comment,
                UserName = $"{review.User.FirstName} {review.User.LastName}",
                IsApproved = review.IsApproved,
                CreatedAt = review.CreatedAt
            };
        }
    }
}
