using GenxAi_Solutions.Models.Background;

namespace GenxAi_Solutions.Services.Interfaces
{
    public interface IJobStore
    {
        Guid Create(string type, int? companyId);
        JobInfo? Get(Guid id);
        void MarkRunning(Guid id);
        void MarkSucceeded(Guid id);
        void MarkFailed(Guid id, string error);
    }
}
