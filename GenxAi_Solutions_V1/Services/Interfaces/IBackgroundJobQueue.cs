namespace GenxAi_Solutions_V1.Services.Interfaces
{
    public interface IBackgroundJobQueue
    {
        ValueTask EnqueueAsync(Guid jobId, Func<CancellationToken, Task> workItem);
        IAsyncEnumerable<(Guid jobId, Func<CancellationToken, Task> workItem)> DequeueAsync(CancellationToken ct);
    }

}
