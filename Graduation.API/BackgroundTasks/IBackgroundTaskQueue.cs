namespace Graduation.API.BackgroundTasks
{
  public interface IBackgroundTaskQueue
  {
    void QueueBackgroundWorkItem(Func<CancellationToken, Task> workItem);
    Task<Func<CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken);
  }
}
