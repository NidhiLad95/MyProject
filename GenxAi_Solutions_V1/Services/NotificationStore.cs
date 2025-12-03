using BOL;
using GenxAi_Solutions_V1.Services.Interfaces;
using Microsoft.Data.SqlClient;
using System.Data;

namespace GenxAi_Solutions_V1.Services
{

    public sealed class NotificationStore : INotificationStore
    {
        private readonly string _conn;
        private readonly ILogger<NotificationStore> _log;
        private readonly IAuditLogger _audit;
        //public NotificationStore(IConfiguration cfg) => _conn = cfg.GetConnectionString("DefaultConnection")!;

        public NotificationStore(IConfiguration cfg, ILogger<NotificationStore> log, IAuditLogger audit)
        {
            _conn = cfg.GetConnectionString("DefaultConnection")!;
            _log = log; _audit = audit;
        }

        // Background watcher (unchanged)
        public async Task<IReadOnlyList<Notification>> GetNewSinceAsync(DateTime sinceUtc, CancellationToken ct)
        {
            _log.LogInformation("GetNewSinceAsync since={SinceUtc:o}", sinceUtc);

            const string sql = @"
SELECT TOP (500)
  Id, CompanyId, UserId, Title, Message, LinkUrl, CreatedAtUtc,
  Process, ModuleName, RefId, Outcome
FROM dbo.AppNotifications WITH (READPAST)
WHERE CreatedAtUtc > @since
ORDER BY CreatedAtUtc ASC, Id ASC;";

            var list = new List<Notification>();
            await using var con = new SqlConnection(_conn);
            await con.OpenAsync(ct);
            await using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.Add(new SqlParameter("@since", SqlDbType.DateTime2) { Value = sinceUtc });

            await using var rd = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);
            while (await rd.ReadAsync(ct))
            {
                list.Add(new Notification
                {
                    Id = rd.GetInt64(0),
                    CompanyId = rd.GetInt32(1),
                    UserId = rd.GetInt32(2),
                    Title = rd.GetString(3),
                    Message = rd.GetString(4),
                    LinkUrl = rd.IsDBNull(5) ? null : rd.GetString(5),
                    CreatedAtUtc = rd.GetDateTime(6),
                    Process = rd.GetString(7),
                    ModuleName = rd.GetString(8),
                    RefId = rd.IsDBNull(9) ? null : rd.GetInt64(9),
                    Outcome = rd.GetString(10)
                });
            }
            _log.LogInformation("GetNewSinceAsync returned {Count} items", list.Count);
            return list;
        }

        // Key method: unread across ALL memberships
        public async Task<IReadOnlyList<Notification>> GetUnreadAfterIdAllAsync(
            int userId,
            IReadOnlyList<int> companyIds,
            long? afterId,
            CancellationToken ct)
        {
            _log.LogInformation("GetUnreadAsync user={UserId} companies={Companies} afterId={AfterId}",
           userId, string.Join(",", companyIds), afterId);

            var list = new List<Notification>();
            if (companyIds is not { Count: > 0 }) return list; // nothing to show

            await using var con = new SqlConnection(_conn);
            await con.OpenAsync(ct);

            // Build IN (@c0,@c1,...) safely
            var cParams = companyIds.Select((_, i) => $"@c{i}").ToArray();
            var companyFilter = $"AND CompanyId IN ({string.Join(",", cParams)})";

            string sql;
            if (afterId.HasValue)
            {
                sql = $@"
SELECT TOP (200)
  Id, CompanyId, Title, Message, LinkUrl, CreatedAtUtc,
  Process, ModuleName, RefId, Outcome
FROM dbo.AppNotifications WITH (READPAST)
WHERE UserId=@u {companyFilter} AND Id > @after
ORDER BY Id ASC;";
            }
            else
            {
                sql = $@"
SELECT TOP (50)
  Id, CompanyId, Title, Message, LinkUrl, CreatedAtUtc,
  Process, ModuleName, RefId, Outcome
FROM dbo.AppNotifications WITH (READPAST)
WHERE UserId=@u {companyFilter} AND IsRead=0
ORDER BY Id DESC;";
            }

            await using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.Add(new SqlParameter("@u", SqlDbType.Int) { Value = userId });
            if (afterId.HasValue)
                cmd.Parameters.Add(new SqlParameter("@after", SqlDbType.BigInt) { Value = afterId.Value });
            for (int i = 0; i < companyIds.Count; i++)
                cmd.Parameters.Add(new SqlParameter($"@c{i}", SqlDbType.Int) { Value = companyIds[i] });

            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct))
            {
                list.Add(new Notification
                {
                    Id = rd.GetInt64(0),
                    CompanyId = rd.GetInt32(1),
                    Title = rd.GetString(2),
                    Message = rd.GetString(3),
                    LinkUrl = rd.IsDBNull(4) ? null : rd.GetString(4),
                    CreatedAtUtc = rd.GetDateTime(5),
                    Process = rd.GetString(6),
                    ModuleName = rd.GetString(7),
                    RefId = rd.IsDBNull(8) ? null : rd.GetInt64(8),
                    Outcome = rd.GetString(9),
                    UserId = userId
                });
            }
            _log.LogInformation("GetUnreadAsync returned {Count} items", list.Count);
            return list;
        }

        // Mark read only within user + his company memberships
        public async Task<int> MarkReadAsync(
            IReadOnlyList<int> companyIds,
            int userId,
            IEnumerable<long> ids,
            CancellationToken ct)
        {
            const int BATCH = 100;
            var total = 0;
            await using var con = new SqlConnection(_conn);
            await con.OpenAsync(ct);

            var batch = new List<long>(BATCH);
            foreach (var id in ids)
            {
                batch.Add(id);
                if (batch.Count == BATCH)
                {
                    total += await ExecBatch(con, companyIds, userId, batch, ct);
                    batch.Clear();
                }
            }
            if (batch.Count > 0)
                total += await ExecBatch(con, companyIds, userId, batch, ct);
            _log.LogInformation("MarkReadAsync user={UserId} updated={Updated} ids={Ids}",
            userId, total, string.Join(",", ids));

            _audit.LogGeneralAudit("Notification.MarkRead", $"user:{userId}", "-", $"updated={total};ids={ids.Count()}");

            return total;
        }

        private static async Task<int> ExecBatch(
            SqlConnection con,
            IReadOnlyList<int> companyIds,
            int userId,
            List<long> ids,
            CancellationToken ct)
        {
            var idParams = string.Join(",", ids.Select((_, i) => $"@id{i}"));
            var cmpParams = string.Join(",", companyIds.Select((_, i) => $"@cmp{i}"));

            var sql = $@"
UPDATE n SET IsRead=1
FROM dbo.AppNotifications n
WHERE n.UserId=@u
  AND n.Id IN ({idParams})
  AND n.CompanyId IN ({cmpParams});";

            await using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.Add(new SqlParameter("@u", SqlDbType.Int) { Value = userId });
            for (int i = 0; i < ids.Count; i++)
                cmd.Parameters.Add(new SqlParameter($"@id{i}", SqlDbType.BigInt) { Value = ids[i] });
            for (int i = 0; i < companyIds.Count; i++)
                cmd.Parameters.Add(new SqlParameter($"@cmp{i}", SqlDbType.Int) { Value = companyIds[i] });

            return await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}
