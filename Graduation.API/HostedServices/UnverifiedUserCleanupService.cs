using Graduation.DAL.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Graduation.API.HostedServices
{
    public class UnverifiedUserCleanupService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<UnverifiedUserCleanupService> _logger;

        public UnverifiedUserCleanupService(IServiceScopeFactory scopeFactory,
            ILogger<UnverifiedUserCleanupService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromHours(24), stoppingToken);

                    using var scope = _scopeFactory.CreateScope();
                    var userManager = scope.ServiceProvider
                        .GetRequiredService<UserManager<AppUser>>();

                    var cutoff = DateTime.UtcNow.AddHours(-24);
                    var unverified = await userManager.Users
                        .Where(u => !u.EmailConfirmed && u.CreatedAt <= cutoff)
                        .ToListAsync(stoppingToken);

                    foreach (var user in unverified)
                        await userManager.DeleteAsync(user);

                    _logger.LogInformation("Cleaned up {Count} unverified users", unverified.Count);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during unverified user cleanup");
                }
            }
        }
    }
}