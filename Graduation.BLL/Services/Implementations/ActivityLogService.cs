using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Entities;
using Graduation.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Graduation.BLL.DTOs.Admin;

namespace Graduation.BLL.Services.Implementations
{
    public class ActivityLogService : IActivityLogService
    {
        private readonly IUnitOfWork _uow;

        public ActivityLogService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task LogAsync(string adminId, string action, string entityType, string? entityIdentifier, string description)
        {
            _uow.Repository<ActivityLog>().Add(new ActivityLog
            {
                AdminId = adminId,
                Action = action,
                EntityType = entityType,
                EntityIdentifier = entityIdentifier,
                Description = description,
                CreatedAt = DateTime.UtcNow,
            });
            await _uow.SaveChangesAsync();
        }

        public async Task<List<RecentActivityDto>> GetRecentActivitiesAsync(int count = 10)
        {
            return await _uow.Repository<ActivityLog>().Query()
                .Include(a => a.Admin)
                .OrderByDescending(a => a.CreatedAt)
                .Take(count)
                .Select(a => new RecentActivityDto
                {
                    Type = a.EntityType,
                    Description = a.Description,
                    Timestamp = a.CreatedAt,
                    Link = GetLink(a.EntityType, a.EntityIdentifier),
                })
                .ToListAsync();
        }

        private static string? GetLink(string entityType, string? identifier)
        {
            if (string.IsNullOrEmpty(identifier)) return null;
            return entityType switch
            {
                "User" => $"/admin/users/{identifier}",
                "Vendor" => $"/admin/vendors/{identifier}",
                "Category" => $"/admin/categories/{identifier}",
                "Product" => $"/admin/products/{identifier}",
                "Review" => $"/admin/review-reports/{identifier}",
                _ => null,
            };
        }
    }
}
