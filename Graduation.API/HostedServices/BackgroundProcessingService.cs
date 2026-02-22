using Shared.BackgroundTasks;
using Microsoft.Extensions.Hosting;

namespace Graduation.API.HostedServices
{
  public class BackgroundProcessingService : BackgroundService
  {
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly ILogger<BackgroundProcessingService> _logger;

    public BackgroundProcessingService(IBackgroundTaskQueue taskQueue, ILogger<BackgroundProcessingService> logger)
    {
      _taskQueue = taskQueue;
      _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      _logger.LogInformation("BackgroundProcessingService started");

      while (!stoppingToken.IsCancellationRequested)
      {
        try
        {
          var workItem = await _taskQueue.DequeueAsync(stoppingToken);

          // Retry logic with exponential backoff
          const int maxAttempts = 3;
          var attempt = 0;
          var succeeded = false;

          while (!succeeded && attempt < maxAttempts && !stoppingToken.IsCancellationRequested)
          {
            attempt++;
            try
            {
              await workItem(stoppingToken);
              succeeded = true;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
              throw;
            }
            catch (Exception ex)
            {
              _logger.LogWarning(ex, "Background work item failed on attempt {Attempt}/{MaxAttempts}", attempt, maxAttempts);

              if (attempt >= maxAttempts)
              {
                // Dead-letter: write details to a simple dead-letter log file
                try
                {
                  var deadLetterDir = Path.Combine(AppContext.BaseDirectory, "logs", "deadletter");
                  Directory.CreateDirectory(deadLetterDir);
                  var file = Path.Combine(deadLetterDir, $"deadletter-{DateTime.UtcNow:yyyyMMddHHmmssfff}.log");
                  var msg = $"[{DateTime.UtcNow:O}] Background work item failed after {maxAttempts} attempts. Exception: {ex}\n";
                  File.WriteAllText(file, msg);
                  _logger.LogError(ex, "Background work item moved to dead-letter: {File}", file);
                }
                catch (Exception writeEx)
                {
                  _logger.LogError(writeEx, "Failed to write dead-letter file");
                }
              }
              else
              {
                // exponential backoff
                var delayMs = (int)(Math.Pow(2, attempt) * 1000);
                await Task.Delay(delayMs, stoppingToken);
              }
            }
          }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
          // shutting down
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Error occurred executing background work item");
        }
      }
    }
  }
}
