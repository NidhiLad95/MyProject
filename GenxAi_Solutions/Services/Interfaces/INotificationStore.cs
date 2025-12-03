using BOL;

namespace GenxAi_Solutions.Services.Interfaces
{
    public interface INotificationStore
    {
        Task<IReadOnlyList<Notification>> GetNewSinceAsync(DateTime sinceUtc, CancellationToken ct);
        Task<IReadOnlyList<Notification>> GetUnreadAfterIdAllAsync(int userId, IReadOnlyList<int> companyIds, long? afterId, CancellationToken ct);
        Task<int> MarkReadAsync(IReadOnlyList<int> companyIds, int userId, IEnumerable<long> ids, CancellationToken ct);
    }
}
