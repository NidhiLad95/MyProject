namespace GenxAi_Solutions.Models.Background
{
    // Services/Background/JobInfo.cs
    public class JobInfo
    {
        public Guid JobId { get; init; }
        public string Type { get; init; } = "";
        public int? CompanyId { get; init; }
        public JobStatus Status { get; set; } = JobStatus.Queued;
        public string? Error { get; set; }
        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
