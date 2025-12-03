namespace GenxAi_Solutions.Services.Interfaces
{
    public interface IBackgroundJobQueue
    {
        ValueTask EnqueueAsync(Guid jobId, Func<CancellationToken, Task> workItem);
        IAsyncEnumerable<(Guid jobId, Func<CancellationToken, Task> workItem)> DequeueAsync(CancellationToken ct);
    }

}
