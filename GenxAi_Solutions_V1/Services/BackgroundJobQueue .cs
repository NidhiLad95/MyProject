using GenxAi_Solutions_V1.Services.Interfaces;
using System.Threading.Channels;

namespace GenxAi_Solutions_V1.Services
{
    public class BackgroundJobQueue : IBackgroundJobQueue
    {
        private readonly ILogger<BackgroundJobQueue> _log;
        private readonly IAuditLogger _audit;

        private readonly Channel<(Guid, Func<CancellationToken, Task>)> _queue =
            Channel.CreateUnbounded<(Guid, Func<CancellationToken, Task>)>();

        public BackgroundJobQueue(ILogger<BackgroundJobQueue> log, IAuditLogger audit)
        { _log = log; _audit = audit; }

        //public ValueTask EnqueueAsync(Guid jobId, Func<CancellationToken, Task> workItem)
        //    => _queue.Writer.WriteAsync((jobId, workItem));
        public ValueTask EnqueueAsync(Guid jobId, Func<CancellationToken, Task> workItem)
        {
            _log.LogInformation("Enqueued job {JobId}", jobId);
            _audit.LogGeneralAudit("JobEnqueued", "system", "-", $"jobId={jobId}");

            return _queue.Writer.WriteAsync((jobId, workItem));
        }

        public IAsyncEnumerable<(Guid jobId, Func<CancellationToken, Task> workItem)> DequeueAsync(CancellationToken ct)
            => _queue.Reader.ReadAllAsync(ct);

    }
}
