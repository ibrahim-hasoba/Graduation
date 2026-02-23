using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Shared.BackgroundTasks;

namespace Graduation.API.BackgroundTasks
{
    public class BackgroundTaskQueue : IBackgroundTaskQueue
    {
        private readonly Channel<Func<CancellationToken, Task>> _queue;
        private readonly ILogger<BackgroundTaskQueue>? _logger;

        public BackgroundTaskQueue(int capacity = 100, ILogger<BackgroundTaskQueue>? logger = null)
        {
            var options = new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest
            };

            _queue = Channel.CreateBounded<Func<CancellationToken, Task>>(options);
            _logger = logger;
        }

        
        public void QueueBackgroundWorkItem(Func<CancellationToken, Task> workItem)
        {
            if (workItem == null) throw new ArgumentNullException(nameof(workItem));

            if (!_queue.Writer.TryWrite(workItem))
            {
                _logger?.LogError(
                    "BackgroundTaskQueue: failed to enqueue work item — channel may be completed. " +
                    "The task has been dropped. Consider increasing queue capacity.");
            }
            else if (_queue.Reader.Count >= _queue.Reader.Count * 0.8)
            {
                _logger?.LogWarning(
                    "BackgroundTaskQueue is at high capacity ({Count} items). " +
                    "Consider scaling the background worker.", _queue.Reader.Count);
            }
        }

        public async Task<Func<CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken)
        {
            return await _queue.Reader.ReadAsync(cancellationToken);
        }
    }
}
