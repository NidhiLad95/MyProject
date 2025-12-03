using GenxAi_Solutions_V1.Services.Interfaces;
using Microsoft.Data.SqlClient;
using System.Data;

namespace GenxAi_Solutions_V1.Services
{
    public class SqlConfigRepository : ISqlConfigRepository
    {
        private readonly string _cs;
        public SqlConfigRepository(IConfiguration cfg)
        {
            _cs = cfg.GetConnectionString("DefaultConnection")!;
        }

        public async Task<SqlAnalyticsConfig?> GetByCompanyIdAsync(int companyId, CancellationToken ct)
        {
            const string sql = @"
SELECT TOP(1) DatabaseName, B.CompanyName, ConnectionString, SchemaName,TablesSelected,ViewsSelected,PromptConfiguration,A.CreatedBy
FROM dbo.SQLAnalyticsConfiguration A WITH (NOLOCK)
INNER JOIN dbo.CompanyProfile B WITH (NOLOCK) ON A.CompanyID=B.CompanyID
WHERE A.IsDeleted = 0 AND A.IsActive = 1 AND A.CompanyID = @CompanyID
ORDER BY ConfigID DESC;";

            using var conn = new SqlConnection(_cs);
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@CompanyID", SqlDbType.Int).Value = companyId;
            await conn.OpenAsync(ct);
            using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) return null;

            return new SqlAnalyticsConfig(
                r["DatabaseName"] as string,
                r["CompanyName"] as string,
                r["ConnectionString"] as string,
                r["SchemaName"] as string,
                r["TablesSelected"] as string,
                r["ViewsSelected"] as string,
                r["PromptConfiguration"] as string,
                r["CreatedBy"] as int?
            );
        }

        public async Task<List<FileConfigRow>> GetFilesByCompanyIdAsync(int companyId, CancellationToken ct)
        {
           

            const string sql = @"
SELECT
    FileConfigID,
    FileName,
    FilePath,
    A.Description,
    PromptConfiguration,
    UploadedAt,
    A.CompanyID,
    B.CompanyName,
    A.CreatedBy
FROM dbo.FileAnalyticsConfiguration A WITH (NOLOCK)
INNER JOIN dbo.CompanyProfile B WITH (NOLOCK) ON A.CompanyID=B.CompanyID
WHERE
    A.IsDeleted = 0
    AND A.IsActive = 1
    AND A.CompanyID = @CompanyID
ORDER BY A.UploadedAt DESC, A.FileConfigID DESC;";

            var list = new List<FileConfigRow>();

            await using var conn = new SqlConnection(_cs);
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@CompanyID", SqlDbType.Int).Value = companyId;

            await conn.OpenAsync(ct);
            await using var r = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection, ct);

            if (!r.HasRows)
                return list; // return empty list instead of null

            // cache ordinals once
            int oFileConfigID = r.GetOrdinal("FileConfigID");
            int oFileName = r.GetOrdinal("FileName");
            int oFilePath = r.GetOrdinal("FilePath");
            int oDescription = r.GetOrdinal("Description");
            int oPromptConfiguration = r.GetOrdinal("PromptConfiguration");
            int oUploadedAt = r.GetOrdinal("UploadedAt");
            int oCompanyID = r.GetOrdinal("CompanyID");
            int oCompanyName = r.GetOrdinal("CompanyName");
            int oCreatedBy = r.GetOrdinal("CreatedBy");

            // iterate rows
            for (; await r.ReadAsync(ct);)
            {
                string? fileName = r.IsDBNull(oFileName) ? null : r.GetString(oFileName);
                if (string.IsNullOrWhiteSpace(fileName)) fileName = null;

                list.Add(new FileConfigRow(
                    FileConfigID: r.GetInt32(oFileConfigID),
                    FileName: fileName,
                    FilePath: r.IsDBNull(oFilePath) ? null : r.GetString(oFilePath),
                    Description: r.IsDBNull(oDescription) ? null : r.GetString(oDescription),
                    PromptConfiguration: r.IsDBNull(oPromptConfiguration) ? null : r.GetString(oPromptConfiguration),
                    UploadedAt: r.IsDBNull(oUploadedAt) ? (DateTime?)null : r.GetDateTime(oUploadedAt),
                    CompanyID: r.GetInt32(oCompanyID),
                    CompanyName: r.IsDBNull(oCompanyName) ? null : r.GetString(oCompanyName),
                    CreatedBy: r.GetInt32(oCreatedBy) 

                ));
            }

            return list;
        }

        public async Task SaveVectorDbNameAsync(int companyId, string dbFileName, CancellationToken ct)
        {
            // You can store this in SQLAnalyticsConfiguration.DatabaseName, or another column you prefer
            const string sql = @"
UPDATE dbo.SQLAnalyticsConfiguration
   SET SQLitedbName = @DbName, UpdatedOn = getdate(), flgSave=2
 WHERE IsDeleted = 0 AND IsActive = 1 AND CompanyID = @CompanyID;
";

            using var conn = new SqlConnection(_cs);
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@DbName", SqlDbType.NVarChar, 256).Value = dbFileName;
            cmd.Parameters.Add("@CompanyID", SqlDbType.Int).Value = companyId;
            await conn.OpenAsync(ct);
            try
            {
                await cmd.ExecuteNonQueryAsync(ct);
            }catch(Exception ex)
            {
                throw ex;
            }
        }

        public async Task SavePDFVectorDbNameAsync(int companyId, string dbFileName, CancellationToken ct)
        {
            // You can store this in SQLAnalyticsConfiguration.DatabaseName, or another column you prefer
            const string sql = @"
UPDATE dbo.FileAnalyticsConfiguration
   SET SQLiteDBName = @DbName, UpdatedOn = getdate(),flgSave=2
 WHERE IsDeleted = 0 AND IsActive = 1 AND CompanyID = @CompanyID;
";

            using var conn = new SqlConnection(_cs);
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@DbName", SqlDbType.NVarChar, 256).Value = dbFileName;
            cmd.Parameters.Add("@CompanyID", SqlDbType.Int).Value = companyId;
            await conn.OpenAsync(ct);
            try
            {
                await cmd.ExecuteNonQueryAsync(ct);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task ExecuteQueryAsync( string connstr,string sqlqry,  CancellationToken ct)
        {
            // You can store this in SQLAnalyticsConfiguration.DatabaseName, or another column you prefer
            
            using var conn = new SqlConnection(connstr);
            using var cmd = new SqlCommand(sqlqry, conn);
            await conn.OpenAsync(ct);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        public async Task UpdateAnalyticsStatusAsync(int? companyId, string type, CancellationToken ct)
        {
            using var conn = new SqlConnection(_cs);
            using var cmd = new SqlCommand("Usp_UpdateSeedingStatus", conn);
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.Add("@CompanyID", SqlDbType.Int).Value = companyId;
            //cmd.Parameters.Add("@CompanyId", SqlDbType.Int).Value = (object?)companyId ?? DBNull.Value;
            cmd.Parameters.Add("@Type", SqlDbType.NVarChar, 50).Value = type;

            await conn.OpenAsync(ct);
            try
            {
                await cmd.ExecuteNonQueryAsync(ct);
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        // Add at the bottom of the same class (uses ADO.NET only)
        public async Task<long> InsertNotificationAsync(
            int companyId,
            int userId,
            string title,
            string message,
            string? linkUrl,
            string process,         // e.g., "Seeding"
            string moduleName,      // e.g., "SQLAnalytics" or "FileAnalytics"
            long? refId,
            string outcome,         // "success" | "fail"
            CancellationToken ct)
        {
            const string sql = @"
INSERT INTO dbo.AppNotifications
  (CompanyId, UserId, Title, Message, LinkUrl, CreatedAtUtc, IsRead, Process, ModuleName, RefId, Outcome)
OUTPUT INSERTED.Id
VALUES
  (@c, @u, @t, @m, @l, SYSUTCDATETIME(), 0, @p, @mod, @rid, @o);";

           using var con = new SqlConnection(_cs);
           //con.OpenAsync(ct);
           using var cmd = new SqlCommand(sql, con);

            cmd.Parameters.Add(new SqlParameter("@c", System.Data.SqlDbType.Int) { Value = companyId });
            cmd.Parameters.Add(new SqlParameter("@u", System.Data.SqlDbType.Int) { Value = userId });
            cmd.Parameters.Add(new SqlParameter("@t", System.Data.SqlDbType.NVarChar, 200) { Value = title });
            cmd.Parameters.Add(new SqlParameter("@m", System.Data.SqlDbType.NVarChar, -1) { Value = message });
            cmd.Parameters.Add(new SqlParameter("@l", System.Data.SqlDbType.NVarChar, 500) { Value = (object?)linkUrl ?? DBNull.Value });
            cmd.Parameters.Add(new SqlParameter("@p", System.Data.SqlDbType.NVarChar, 100) { Value = process });
            cmd.Parameters.Add(new SqlParameter("@mod", System.Data.SqlDbType.NVarChar, 150) { Value = moduleName });
            cmd.Parameters.Add(new SqlParameter("@rid", System.Data.SqlDbType.BigInt) { Value = (object?)refId ?? DBNull.Value });
            cmd.Parameters.Add(new SqlParameter("@o", System.Data.SqlDbType.NVarChar, 10) { Value = outcome });
            await con.OpenAsync(ct);
            var idObj = await cmd.ExecuteScalarAsync(ct);
            return (idObj is long id) ? id : Convert.ToInt64(idObj);
        }
    }
}
