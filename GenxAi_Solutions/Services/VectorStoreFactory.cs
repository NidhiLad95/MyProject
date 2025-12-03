using GenxAi_Solutions.Services.Interfaces;
using GenxAi_Solutions.Utils;
using Microsoft.Data.Sqlite;
using Microsoft.SemanticKernel.Connectors.SqliteVec;

namespace GenxAi_Solutions.Services
{
    public class VectorStoreFactory : IVectorStoreFactory
    {
        private readonly ILogger<VectorStoreFactory> _logger;
        private readonly ILogger<SQLiteVectorStore> _sqliteLogger;

        public VectorStoreFactory(ILogger<VectorStoreFactory> logger, ILogger<SQLiteVectorStore> sqliteLogger)
        {
            _logger = logger;
            _sqliteLogger = sqliteLogger;
        }

        public SQLiteVectorStore Create(string dbName)
        {
            var dbPath = Path.Combine(AppContext.BaseDirectory, dbName);
            _logger.LogInformation("Creating SQLiteVectorStore at {DbPath}", dbPath);
            return new SQLiteVectorStore(dbPath, _sqliteLogger);
        }
        //public SqliteVectorStore PdfCreate(string dbName)
        //{
        //    var dbPath = Path.Combine(AppContext.BaseDirectory, dbName);
        //    return new SqliteVectorStore(dbPath);
        //}
        public SqliteVectorStore PdfCreate(string dbName)
        {
            // If the caller already passed a connection string, use it as-is.
            if (!string.IsNullOrWhiteSpace(dbName) &&
                dbName.IndexOf("data source=", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new SqliteVectorStore(dbName);
            }

            // Otherwise, treat it as a file name and build a connection string.
            var dbPath = Path.Combine(AppContext.BaseDirectory, dbName);

            var dir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var cs = new SqliteConnectionStringBuilder
            {
                DataSource = dbPath
                // You can add options if you like, e.g.: Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString();

            //// Optional: quick sanity log to help future debugging
            //Console.WriteLine($"[PdfCreate] DataSource={dbPath}  Exists={File.Exists(dbPath)}");

            _logger.LogInformation("PdfCreate DataSource={DbPath} Exists={Exists}", dbPath, File.Exists(dbPath));


            return new SqliteVectorStore(cs);
        }
    }
}
