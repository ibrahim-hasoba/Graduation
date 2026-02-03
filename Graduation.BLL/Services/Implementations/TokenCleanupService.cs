using Graduation.BLL.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class TokenCleanupService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<TokenCleanupService> _logger;

    public TokenCleanupService(IServiceProvider services, ILogger<TokenCleanupService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Token Cleanup Service is running.");

            using (var scope = _services.CreateScope())
            {
                var refreshTokenService = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();
                await refreshTokenService.CleanupExpiredTokensAsync();
            }

            // Run once every 24 hours
            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }
}