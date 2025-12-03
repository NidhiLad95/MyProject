//using GenxAi_Solutions.Services.Hubs;
//using GenxAi_Solutions.Services.Interfaces;
//using Microsoft.AspNetCore.SignalR;
//using System.ComponentModel.Design;

//namespace GenxAi_Solutions.Services.Background
//{
//    public class QueuedWorker : BackgroundService
//    {
//        private readonly IBackgroundJobQueue _queue;
//        private readonly IJobStore _store;
//        private readonly IHubContext<SemanticHub> _hub;

//        public QueuedWorker(IBackgroundJobQueue queue, IJobStore store, IHubContext<SemanticHub> hub)
//        {
//            _queue = queue;
//            _store = store;
//            _hub = hub;
//        }

//        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//        {
//            await foreach (var (jobId, work) in _queue.DequeueAsync(stoppingToken))
//            {
//                _store.MarkRunning(jobId);
//                try
//                {
//                    await _hub.Clients.Group($"company-{companyId}")
//    .SendAsync("SeedStatus", new { status = "Succeeded", companyId, jobId }, stoppingToken);

//                    await work(stoppingToken);
//                    _store.MarkSucceeded(jobId);
//                    await _hub.Clients.All.SendAsync("JobCompleted", jobId.ToString(), cancellationToken: stoppingToken);
//                }
//                catch (Exception ex)
//                {
//                    _store.MarkFailed(jobId, ex.Message);
//                    await _hub.Clients.All.SendAsync("JobFailed", jobId.ToString(), ex.Message, cancellationToken: stoppingToken);
//                }
//            }
//        }
//    }
//}


using GenxAi_Solutions_V1.Services.Hubs;
using GenxAi_Solutions_V1.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
//using Microsoft.Extensions.Hosting;

namespace GenxAi_Solutions_V1.Services.Background
{
    /// <summary>
    /// Background worker that executes queued jobs and broadcasts status via SignalR.
    /// </summary>
    public sealed class QueuedWorker : BackgroundService
    {
        private readonly IBackgroundJobQueue _queue;
        private readonly IJobStore _store;
        private readonly IHubContext<SemanticHub> _hub;
        private readonly ILogger<QueuedWorker> _logger;
        private readonly ISqlConfigRepository _sqlRepo;

        public QueuedWorker(
            IBackgroundJobQueue queue,
            IJobStore store,
            IHubContext<SemanticHub> hub,
            ILogger<QueuedWorker> logger, ISqlConfigRepository sqlRepo)
        {
            _queue = queue;
            _store = store;
            _hub = hub;
            _logger = logger;
            _sqlRepo = sqlRepo;
        }

        //protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        //{
        //    _logger.LogInformation("QueuedWorker started.");

        //    while (!stoppingToken.IsCancellationRequested)
        //    {
        //        BackgroundWorkItem? item = null;

        //        try
        //        {
        //            // Wait for next job
        //            item = await _queue.DequeueAsync(stoppingToken);
        //            if (item is null) continue;

        //            var job = _store.Get(item.JobId);
        //            if (job is null)
        //            {
        //                _logger.LogWarning("Dequeued job {JobId} not found in store.", item.JobId);
        //                continue;
        //            }

        //            var companyId = job.CompanyId ?? 0;
        //            var group = GroupName(companyId);

        //            // Mark + broadcast "Running"
        //            _store.MarkRunning(item.JobId);
        //            await _hub.Clients.Group(group).SendAsync("SeedStatus", new
        //            {
        //                jobId = item.JobId,
        //                companyId,
        //                type = job.Type,
        //                status = "Running",
        //                at = DateTimeOffset.UtcNow
        //            }, stoppingToken);

        //            // Execute user work
        //            await item.Work(stoppingToken);

        //            // Mark + broadcast "Succeeded"
        //            _store.MarkSucceeded(item.JobId);
        //            await _hub.Clients.Group(group).SendAsync("SeedStatus", new
        //            {
        //                jobId = item.JobId,
        //                companyId,
        //                type = job.Type,
        //                status = "Succeeded",
        //                at = DateTimeOffset.UtcNow
        //            }, stoppingToken);

        //            _logger.LogInformation("Job {JobId} for company {CompanyId} completed.", item.JobId, companyId);
        //        }
        //        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        //        {
        //            // graceful shutdown
        //            break;
        //        }
        //        catch (Exception ex)
        //        {
        //            // Fail + broadcast "Failed"
        //            if (item is not null)
        //            {
        //                var job = _store.Get(item.JobId);
        //                var companyId = job?.CompanyId ?? 0;

        //                _store.MarkFailed(item.JobId, ex.Message);

        //                try
        //                {
        //                    await _hub.Clients.Group(GroupName(companyId)).SendAsync("SeedStatus", new
        //                    {
        //                        jobId = item.JobId,
        //                        companyId,
        //                        type = job?.Type,
        //                        status = "Failed",
        //                        error = ex.Message,
        //                        at = DateTimeOffset.UtcNow
        //                    }, stoppingToken);
        //                }
        //                catch { /* swallow hub errors to not crash loop */ }
        //            }

        //            _logger.LogError(ex, "Job execution failed.");
        //        }
        //    }

        //    _logger.LogInformation("QueuedWorker stopping.");
        //}

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("QueuedWorker started.");

            try
            {
                // DequeueAsync returns IAsyncEnumerable<(Guid jobId, Func<CancellationToken, Task> workItem)>
                await foreach (var tuple in _queue.DequeueAsync(stoppingToken)
                                                   .WithCancellation(stoppingToken))
                {
                    var (jobId, work) = tuple; // tuple deconstruction

                    var job = _store.Get(jobId);
                    if (job is null)
                    {
                        _logger.LogWarning("Dequeued job {JobId} not found in store.", jobId);
                        continue;
                    }

                    var companyId = job.CompanyId ?? 0;
                    var group = GroupName(companyId);

                    try
                    {
                        // mark + broadcast Running
                        _store.MarkRunning(jobId);
                        await _hub.Clients.Group(group).SendAsync("SeedStatus", new
                        {
                            jobId,
                            companyId,
                            type = job.Type,
                            status = "Running",
                            at = DateTimeOffset.UtcNow
                        }, stoppingToken);

                        // do the work
                        await work(stoppingToken);

                        // mark + broadcast Succeeded
                        _store.MarkSucceeded(jobId);

                        await _sqlRepo.UpdateAnalyticsStatusAsync(job.CompanyId ?? 0, job.Type, stoppingToken);

                        await _hub.Clients.Group(group).SendAsync("SeedStatus", new
                        {
                            jobId,
                            companyId,
                            type = job.Type,
                            status = "Succeeded",
                            at = DateTimeOffset.UtcNow
                        }, stoppingToken);

                        _logger.LogInformation("Job {JobId} for company {CompanyId} completed.", jobId, companyId);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        // graceful shutdown
                        break;
                    }
                    catch (Exception ex)
                    {
                        // mark + broadcast Failed
                        _store.MarkFailed(jobId, ex.Message);
                        try
                        {
                            await _hub.Clients.Group(group).SendAsync("SeedStatus", new
                            {
                                jobId,
                                companyId,
                                type = job.Type,
                                status = "Failed",
                                error = ex.Message,
                                at = DateTimeOffset.UtcNow
                            }, stoppingToken);
                        }
                        catch { /* don't crash loop on hub failure */ }

                        _logger.LogError(ex, "Job {JobId} failed for company {CompanyId}.", jobId, companyId);
                    }
                }
            }
            finally
            {
                _logger.LogInformation("QueuedWorker stopping.");
            }
        }

        private static string GroupName(int companyId) => $"company-{companyId}";


        //private static string GroupName(int companyId) => $"company-{companyId}";
    }

    /// <summary>
    /// Typical queue payload. Ensure your IBackgroundJobQueue returns this.
    /// </summary>
    public sealed record BackgroundWorkItem(Guid JobId, Func<CancellationToken, Task> Work);
}
