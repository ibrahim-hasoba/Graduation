using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Shared.BackgroundTasks;

namespace Graduation.API.BackgroundTasks
{
    public class BackgroundTaskQueue : IBackgroundTaskQueue
    {
        private readonly Channel<Func<CancellationToken, Task>> _queue;
        private readonly ILogger<BackgroundTaskQueue>? _logger;
        private readonly int _capacity;

        public BackgroundTaskQueue(int capacity = 100, ILogger<BackgroundTaskQueue>? logger = null)
        {
            _capacity = capacity;
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
                    "BackgroundTaskQueue: failed to enqueue work item — channel is full and oldest item " +
                    "was dropped (DropOldest). Consider increasing queue capacity beyond {Capacity}.",
                    _capacity);
            }
            
            else if (_queue.Reader.Count >= _capacity * 0.8)
            {
                _logger?.LogWarning(
                    "BackgroundTaskQueue is at high capacity ({Count}/{Capacity} items). " +
                    "Consider scaling the background worker.",
                    _queue.Reader.Count, _capacity);
            }
        }

        public async Task<Func<CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken)
        {
            return await _queue.Reader.ReadAsync(cancellationToken);
        }
    }
}
