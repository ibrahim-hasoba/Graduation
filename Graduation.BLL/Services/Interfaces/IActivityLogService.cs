using Shared.DTOs.Admin;

namespace Graduation.BLL.Services.Interfaces
{
    public interface IActivityLogService
    {
        Task LogAsync(string adminId, string action, string entityType, string? entityIdentifier, string description);
        Task<List<RecentActivityDto>> GetRecentActivitiesAsync(int count = 10);
    }
}
