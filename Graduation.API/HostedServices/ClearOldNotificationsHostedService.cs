using Graduation.BLL.Services.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Graduation.API.HostedServices
{
  public class ClearOldNotificationsHostedService : BackgroundService
  {
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ClearOldNotificationsHostedService> _logger;

    public ClearOldNotificationsHostedService(IServiceScopeFactory scopeFactory, ILogger<ClearOldNotificationsHostedService> logger)
    {
      _scopeFactory = scopeFactory;
      _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      _logger.LogInformation("ClearOldNotificationsHostedService started");

      while (!stoppingToken.IsCancellationRequested)
      {
        try
        {
          using var scope = _scopeFactory.CreateScope();
          var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

          // Keep this conservative: clear notifications older than 30 days
          await notificationService.ClearOldNotificationsAsync(daysOld: 30);
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Error while clearing old notifications");
        }

        // Run once per day
        await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
      }
    }
  }
}
