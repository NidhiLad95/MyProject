using GenxAi_Solutions.Services.Hubs;
using GenxAi_Solutions.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;

namespace GenxAi_Solutions.Services.Background
{
    

    public class NotificationWatcher : BackgroundService
    {
        private readonly INotificationStore _store;
        private readonly IHubContext<NotificationHub> _hub;
        private readonly ILogger<NotificationWatcher> _log;
        private DateTime _lastCheckUtc = DateTime.UtcNow.AddMinutes(-5);

        public NotificationWatcher(ILogger<NotificationWatcher> log,INotificationStore store, IHubContext<NotificationHub> hub)
        { _store = store; _hub = hub; _log = log; }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var delayOnError = TimeSpan.FromSeconds(10);
            while (!stoppingToken.IsCancellationRequested)
            {
                var fresh = await _store.GetNewSinceAsync(_lastCheckUtc, stoppingToken);

                foreach (var n in fresh)
                {
                    await _hub.Clients.Group($"user_{n.UserId}")     // user-targeted push
                        .SendAsync("notify", new
                        {
                            id = n.Id,
                            title = n.Title,
                            message = n.Message,
                            linkUrl = n.LinkUrl,
                            createdAtUtc = n.CreatedAtUtc,
                            process = n.Process,
                            moduleName = n.ModuleName,
                            refId = n.RefId,
                            outcome = n.Outcome,
                            companyId = n.CompanyId
                        }, stoppingToken);
                    // If you also want company broadcast:
                    // await _hub.Clients.Group($"company_{n.CompanyId}").SendAsync("notify", ...);
                }

                if (fresh.Count > 0)
                    _lastCheckUtc = fresh[^1].CreatedAtUtc;

                try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); }
                catch  (SqlException ex) {
                    _log.LogWarning(ex, "SQL error in NotificationWatcher. Will retry in {Delay}s.", delayOnError.TotalSeconds);
                    await Task.Delay(delayOnError, stoppingToken);
                }
            }
        }
    }

}
