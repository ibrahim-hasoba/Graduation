using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Graduation.BLL.DTOs.Admin;

namespace Graduation.BLL.Services.Implementations
{
    public class ActivityLogService : IActivityLogService
    {
        private readonly DatabaseContext _context;

        public ActivityLogService(DatabaseContext context)
        {
            _context = context;
        }

        public async Task LogAsync(string adminId, string action, string entityType, string? entityIdentifier, string description)
        {
            _context.ActivityLogs.Add(new ActivityLog
            {
                AdminId = adminId,
                Action = action,
                EntityType = entityType,
                EntityIdentifier = entityIdentifier,
                Description = description,
                CreatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();
        }

        public async Task<List<RecentActivityDto>> GetRecentActivitiesAsync(int count = 10)
        {
            return await _context.ActivityLogs
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
