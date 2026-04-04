using Graduation.BLL.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Graduation.BLL.BackgroundJobs
{
   
    public class StaleOrderCleanupJob : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);
        private static readonly int TimeoutMinutes = 30;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<StaleOrderCleanupJob> _logger;

        public StaleOrderCleanupJob(
            IServiceScopeFactory scopeFactory,
            ILogger<StaleOrderCleanupJob> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("StaleOrderCleanupJob started — interval {Interval}.", Interval);

            await Task.Delay(Interval, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();

                    _logger.LogInformation("StaleOrderCleanupJob: running cleanup...");
                    await paymentService.CancelStaleOrdersAsync(TimeoutMinutes);
                    _logger.LogInformation("StaleOrderCleanupJob: cleanup complete.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "StaleOrderCleanupJob: unhandled error during cleanup.");
                }

                await Task.Delay(Interval, stoppingToken);
            }
        }
    }
}
