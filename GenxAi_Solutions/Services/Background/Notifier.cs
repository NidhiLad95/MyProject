using GenxAi_Solutions.Services.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;

namespace GenxAi_Solutions.Services.Background
{
    public sealed class Notifier
    {
        private readonly IHubContext<NotificationHub> _hub;
        private readonly string _conn;

        public Notifier(IConfiguration cfg, IHubContext<NotificationHub> hub)
        { _hub = hub; _conn = cfg.GetConnectionString("DefaultConnection")!; }

        public async Task<long> CreateAsync(
            int companyId,
            int userId,
            string title,
            string message,
            string? linkUrl,
            string process,
            string moduleName,
            long? refId,
            string outcome, // "success" | "fail"
            CancellationToken ct)
        {
            const string sql = @"
INSERT INTO dbo.AppNotifications
  (CompanyId, UserId, Title, Message, LinkUrl, Process, ModuleName, RefId, Outcome)
OUTPUT INSERTED.Id, INSERTED.CreatedAtUtc
VALUES
  (@c, @u, @t, @m, @l, @p, @mod, @rid, @o);";

            await using var con = new SqlConnection(_conn);
            await con.OpenAsync(ct);
            await using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@c", companyId);
            cmd.Parameters.AddWithValue("@u", userId);
            cmd.Parameters.AddWithValue("@t", title);
            cmd.Parameters.AddWithValue("@m", message);
            cmd.Parameters.AddWithValue("@l", (object?)linkUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@p", process);
            cmd.Parameters.AddWithValue("@mod", moduleName);
            cmd.Parameters.AddWithValue("@rid", (object?)refId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@o", outcome); // expect "success" or "fail"

            long id = 0; DateTime createdUtc = DateTime.UtcNow;
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            if (await rd.ReadAsync(ct))
            {
                id = rd.GetInt64(0);
                createdUtc = rd.GetDateTime(1);
            }

            var payload = new
            {
                id,
                title,
                message,
                linkUrl,
                createdAtUtc = createdUtc,
                process,
                moduleName,
                refId,
                outcome,
                companyId,
                userId
            };

            await _hub.Clients.Group($"user_{userId}").SendAsync("notify", payload, ct);
            // optionally also broadcast to company:
            // await _hub.Clients.Group($"company_{companyId}").SendAsync("notify", payload, ct);

            return id;
        }
    }

}
