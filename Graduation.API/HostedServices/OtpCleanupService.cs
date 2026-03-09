namespace Graduation.API.HostedServices
{
    using global::Graduation.DAL.Data;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    namespace Graduation.API.HostedServices
    {

        public class OtpCleanupService : BackgroundService
        {
            private readonly IServiceProvider _serviceProvider;
            private readonly ILogger<OtpCleanupService> _logger;
            private readonly TimeSpan _interval;

            public OtpCleanupService(
                IServiceProvider serviceProvider,
                ILogger<OtpCleanupService> logger,
                IConfiguration configuration)
            {
                _serviceProvider = serviceProvider;
                _logger = logger;
                var hours = configuration.GetValue<int>("OtpCleanup:IntervalHours", 24);
                _interval = TimeSpan.FromHours(hours);
            }

            protected override async Task ExecuteAsync(CancellationToken stoppingToken)
            {
                _logger.LogInformation("OtpCleanupService started. Runs every {Interval}.", _interval);

                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(_interval, stoppingToken);

                    try
                    {
                        await CleanupAsync(stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during OTP cleanup");
                    }
                }
            }

            private async Task CleanupAsync(CancellationToken ct)
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

                var deleted = await context.EmailOtps
                    .Where(o => o.ExpiresAt <= DateTime.UtcNow)
                    .ExecuteDeleteAsync(ct);

                if (deleted > 0)
                    _logger.LogInformation("OtpCleanupService removed {Count} expired OTP record(s).", deleted);
            }
        }
    }

}
