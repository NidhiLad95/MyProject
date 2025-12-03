using GenxAi_Solutions_V1.Models.Background;
using GenxAi_Solutions_V1.Services.Interfaces;
using System.Collections.Concurrent;

namespace GenxAi_Solutions_V1.Services.Background
{
    public class InMemoryJobStore : IJobStore
    {
        private readonly ConcurrentDictionary<Guid, JobInfo> _jobs = new();

        public Guid Create(string type, int? companyId)
        {
            var id = Guid.NewGuid();
            _jobs[id] = new JobInfo { JobId = id, Type = type, CompanyId = companyId };
            return id;
        }

        public JobInfo? Get(Guid id) => _jobs.TryGetValue(id, out var j) ? j : null;

        public void MarkRunning(Guid id)
        {
            if (_jobs.TryGetValue(id, out var j))
            {
                j.Status = JobStatus.Running;
                j.StartedAt = DateTime.UtcNow;
            }
        }

        public void MarkSucceeded(Guid id)
        {
            if (_jobs.TryGetValue(id, out var j))
            {
                j.Status = JobStatus.Succeeded;
                j.CompletedAt = DateTime.UtcNow;
            }
        }

        public void MarkFailed(Guid id, string error)
        {
            if (_jobs.TryGetValue(id, out var j))
            {
                j.Status = JobStatus.Failed;
                j.Error = error;
                j.CompletedAt = DateTime.UtcNow;
            }
        }
    }
}
