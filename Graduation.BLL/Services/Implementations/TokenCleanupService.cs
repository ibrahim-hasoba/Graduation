using Graduation.DAL.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Graduation.API.HostedServices
{
    public class TokenCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TokenCleanupService> _logger;
        private readonly TimeSpan _interval;

        public TokenCleanupService(
            IServiceProvider serviceProvider,
            ILogger<TokenCleanupService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;

            var intervalHours = configuration.GetValue<int>("TokenCleanup:IntervalHours", 24);
            _interval = TimeSpan.FromHours(intervalHours);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "TokenCleanupService started. Runs every {Interval}.", _interval);

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(_interval, stoppingToken);

                try
                {
                    await CleanupExpiredTokensAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during token cleanup");
                }
            }
        }

        private async Task CleanupExpiredTokensAsync(CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

            var cutoff = DateTime.UtcNow;

            var deletedCount = await context.RefreshTokens
                .Where(t => t.ExpiresAt <= cutoff)
                .ExecuteDeleteAsync(ct);

            if (deletedCount > 0)
            {
                _logger.LogInformation(
                    "TokenCleanupService removed {Count} expired refresh token(s).", deletedCount);
            }
        }
    }
}
