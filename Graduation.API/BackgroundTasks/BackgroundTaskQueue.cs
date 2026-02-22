using System.Threading.Channels;

using Shared.BackgroundTasks;

namespace Graduation.API.BackgroundTasks
{
  public class BackgroundTaskQueue : Shared.BackgroundTasks.IBackgroundTaskQueue
  {
    private readonly Channel<Func<CancellationToken, Task>> _queue;

    public BackgroundTaskQueue(int capacity = 100)
    {
      var options = new BoundedChannelOptions(capacity)
      {
        FullMode = BoundedChannelFullMode.Wait
      };

      _queue = Channel.CreateBounded<Func<CancellationToken, Task>>(options);
    }

    public void QueueBackgroundWorkItem(Func<CancellationToken, Task> workItem)
    {
      if (workItem == null) throw new ArgumentNullException(nameof(workItem));
      _queue.Writer.TryWrite(workItem);
    }

    public async Task<Func<CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken)
    {
      var workItem = await _queue.Reader.ReadAsync(cancellationToken);
      return workItem;
    }
  }
}
