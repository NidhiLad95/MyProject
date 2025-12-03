#region Old Working Agent

//using GenxAi_Solutions_V1.Models;
//using GenxAi_Solutions_V1.Services.Interfaces;
//using GenxAi_Solutions_V1.Utils;
//using Microsoft.Data.Sqlite;
//using Microsoft.Extensions.AI;
//using Microsoft.SemanticKernel.Connectors.SqliteVec;
//using System.Collections.Concurrent;

//namespace GenxAi_Solutions_V1.Services
//{
//    public class VectorStoreFactory : IVectorStoreFactory
//    {
//        private readonly ILogger<VectorStoreFactory> _logger;
//        //private readonly ILogger<SQLiteVectorStore> _sqliteLogger;
//        private readonly IEmbeddingGenerator<string, Embedding<float>> _embedderNew;
//        private readonly IEmbeddingGenerator<string, Embedding<float>> _embedderpdfNew;
//        // NEW: caches per SQLite file
//        private readonly ConcurrentDictionary<string, SqlVectorStores> _sqlStores = new();
//        private readonly ConcurrentDictionary<string, PdfVectorStores> _pdfStores = new();
//        public VectorStoreFactory(ILogger<VectorStoreFactory> logger,
//            //ILogger<SQLiteVectorStore> sqliteLogger, 
//            IEmbeddingGenerator<string, Embedding<float>> embeddernew, IEmbeddingGenerator<string, Embedding<float>> embedderpdfNew)
//        {
//            _logger = logger;
//            //_sqliteLogger = sqliteLogger;
//            _embedderNew = embeddernew;
//            _embedderpdfNew = embedderpdfNew;

//        }




//        /// <summary>
//        /// As per Agent Framework Logic : Sql 
//        /// </summary>
//        /// <param name="dbName"></param>
//        /// <returns></returns>
//        public SqlVectorStores Create_New1(string dbName)
//        {
//            var dbPath = Path.Combine(AppContext.BaseDirectory, dbName);

//            var dir = Path.GetDirectoryName(dbPath);
//            if (!string.IsNullOrEmpty(dir))
//            {
//                Directory.CreateDirectory(dir);
//            }
//            var options = new SqliteCollectionOptions { EmbeddingGenerator = _embedderNew };
//            var schemas = new SqliteCollection<string, SchemaRecord>(
//        $"Data Source={dbPath}", "sql_schemas", options);
//            var prompts = new SqliteCollection<string, PromptRecord>(
//                $"Data Source={dbPath}", "prompts", options);

//            schemas.EnsureCollectionExistsAsync().GetAwaiter().GetResult();
//            prompts.EnsureCollectionExistsAsync().GetAwaiter().GetResult();
//            _logger.LogInformation("Creating SQLiteVectorStore at {DbPath}", dbPath);


//            return new SqlVectorStores(schemas, prompts);
//        }
//        public PdfVectorStores PdfCreate_New1(string dbName)
//        {

//            var dbPath = Path.Combine(AppContext.BaseDirectory, dbName);

//            var dir = Path.GetDirectoryName(dbPath);
//            if (!string.IsNullOrEmpty(dir))
//            {
//                Directory.CreateDirectory(dir);
//            }

//            var cs = new SqliteConnectionStringBuilder
//            {
//                DataSource = dbPath
//                // You can add options if you like, e.g.: Mode = SqliteOpenMode.ReadWriteCreate
//            }.ToString();
//            var options = new SqliteCollectionOptions { EmbeddingGenerator = _embedderpdfNew };
//            var chapters = new SqliteCollection<string, PdfChapterRecord>($"Data Source={dbPath}", "chapter_index", options);
//            var prompts = new SqliteCollection<string, PromptRecord>($"Data Source={dbPath}", "prompts", options);


//            chapters.EnsureCollectionExistsAsync().GetAwaiter().GetResult();

//            //// Optional: quick sanity log to help future debugging
//            _logger.LogInformation("PdfCreate DataSource={DbPath} Exists={Exists}", dbPath, File.Exists(dbPath));

//            return new PdfVectorStores($"Data Source={dbPath}", chapters, prompts);

//        }

//        public SqlVectorStores Create_New(string dbName)
//        {
//            var dbPath = Path.Combine(AppContext.BaseDirectory, dbName);

//            return _sqlStores.GetOrAdd(dbPath, path =>
//            {
//                var dir = Path.GetDirectoryName(path);
//                if (!string.IsNullOrEmpty(dir))
//                {
//                    Directory.CreateDirectory(dir);
//                }

//                var options = new SqliteCollectionOptions { EmbeddingGenerator = _embedderNew };

//                var schemas = new SqliteCollection<string, SchemaRecord>(
//                    $"Data Source={path}", "sql_schemas", options);
//                var prompts = new SqliteCollection<string, PromptRecord>(
//                    $"Data Source={path}", "prompts", options);

//                // Only done once per DB
//                schemas.EnsureCollectionExistsAsync().GetAwaiter().GetResult();
//                prompts.EnsureCollectionExistsAsync().GetAwaiter().GetResult();

//                _logger.LogInformation("Creating SQLiteVectorStore at {DbPath}", path);

//                return new SqlVectorStores(schemas, prompts);
//            });
//        }

//        public PdfVectorStores PdfCreate_New(string dbName)
//        {
//            var dbPath = Path.Combine(AppContext.BaseDirectory, dbName);

//            return _pdfStores.GetOrAdd(dbPath, path =>
//            {
//                var dir = Path.GetDirectoryName(path);
//                if (!string.IsNullOrEmpty(dir))
//                {
//                    Directory.CreateDirectory(dir);
//                }

//                var cs = new SqliteConnectionStringBuilder
//                {
//                    DataSource = path
//                }.ToString();

//                var options = new SqliteCollectionOptions { EmbeddingGenerator = _embedderpdfNew };

//                var chapters = new SqliteCollection<string, PdfChapterRecord>(cs, "chapter_index", options);
//                var prompts = new SqliteCollection<string, PromptRecord>(cs, "prompts", options);

//                chapters.EnsureCollectionExistsAsync().GetAwaiter().GetResult();

//                _logger.LogInformation("PdfCreate DataSource={DbPath} Exists={Exists}", path, File.Exists(path));

//                return new PdfVectorStores(cs, chapters, prompts);
//            });
//        }
//    }
//}
#endregion

#region New

using GenxAi_Solutions_V1.Models;
using GenxAi_Solutions_V1.Services.Interfaces;
using GenxAi_Solutions_V1.Utils;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.Connectors.SqliteVec;
using System.Collections.Concurrent;

namespace GenxAi_Solutions_V1.Services
{
    public class VectorStoreFactory : IVectorStoreFactory
    {
        private readonly ILogger<VectorStoreFactory> _logger;
        private readonly IEmbeddingGenerator<string, Embedding<float>> _embedderNew;
        private readonly IEmbeddingGenerator<string, Embedding<float>> _embedderpdfNew;

        // Cache stores with Lazy initialization for thread safety
        private readonly ConcurrentDictionary<string, Lazy<SqlVectorStores>> _sqlStores = new();
        private readonly ConcurrentDictionary<string, Lazy<PdfVectorStores>> _pdfStores = new();

        // Track initialization status
        private static bool _sqliteInitialized = false;
        private static readonly object _initLock = new();

        public VectorStoreFactory(
            ILogger<VectorStoreFactory> logger,
            IEmbeddingGenerator<string, Embedding<float>> embeddernew,
            IEmbeddingGenerator<string, Embedding<float>> embedderpdfNew)
        {
            _logger = logger;
            _embedderNew = embeddernew;
            _embedderpdfNew = embedderpdfNew;

            // Initialize SQLite once for performance
            InitializeSqliteOnce();
        }

        /// <summary>
        /// Initialize SQLite once for better performance
        /// </summary>
        private void InitializeSqliteOnce()
        {
            if (_sqliteInitialized) return;

            lock (_initLock)
            {
                if (_sqliteInitialized) return;

                try
                {
                    // Use the most stable SQLite provider
                    SQLitePCL.Batteries.Init();

                    // Enable connection pooling
                    SqliteConnection.ClearAllPools();

                    _sqliteInitialized = true;
                    _logger.LogDebug("SQLite initialized with connection pooling");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "SQLite initialization warning");
                }
            }
        }

        /// <summary>
        /// Optimize SQLite connection string for performance
        /// </summary>
        private string GetOptimizedConnectionString(string dbPath)
        {
            // Use the existing connection string format but with optimizations
            return $"Data Source={dbPath};Pooling=True;Cache=Shared";
        }

        public SqlVectorStores Create_New1(string dbName)
        {
            var dbPath = Path.Combine(AppContext.BaseDirectory, dbName);

            // Use the optimized caching method
            return CreateOptimizedSqlStore(dbPath);
        }

        public PdfVectorStores PdfCreate_New1(string dbName)
        {
            var dbPath = Path.Combine(AppContext.BaseDirectory, dbName);

            // Use the optimized caching method
            return CreateOptimizedPdfStore(dbPath);
        }

        public SqlVectorStores Create_New(string dbName)
        {
            var dbPath = Path.Combine(AppContext.BaseDirectory, dbName);

            // Use the optimized caching method
            return CreateOptimizedSqlStore(dbPath);
        }

        public PdfVectorStores PdfCreate_New(string dbName)
        {
            var dbPath = Path.Combine(AppContext.BaseDirectory, dbName);

            // Use the optimized caching method
            return CreateOptimizedPdfStore(dbPath);
        }

        /// <summary>
        /// Optimized SQL store creation with better caching
        /// </summary>
        private SqlVectorStores CreateOptimizedSqlStore(string dbPath)
        {
            return _sqlStores.GetOrAdd(dbPath, path =>
                new Lazy<SqlVectorStores>(() => CreateSqlStoreInternal(path)))
                .Value;
        }

        /// <summary>
        /// Optimized PDF store creation with better caching
        /// </summary>
        private PdfVectorStores CreateOptimizedPdfStore(string dbPath)
        {
            return _pdfStores.GetOrAdd(dbPath, path =>
                new Lazy<PdfVectorStores>(() => CreatePdfStoreInternal(path)))
                .Value;
        }

        /// <summary>
        /// Internal SQL store creation with performance optimizations
        /// </summary>
        private SqlVectorStores CreateSqlStoreInternal(string dbPath)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var dir = Path.GetDirectoryName(dbPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                // Use optimized connection string
                var connectionString = GetOptimizedConnectionString(dbPath);

                // Configure SQLite for performance on first connection
                ConfigureSqlitePerformance(connectionString);

                var options = new SqliteCollectionOptions
                {
                    EmbeddingGenerator = _embedderNew
                };

                // Create collections
                var schemas = new SqliteCollection<string, SchemaRecord>(connectionString, "sql_schemas", options);
                var prompts = new SqliteCollection<string, PromptRecord>(connectionString, "prompts", options);

                // Initialize collections - use ConfigureAwait(false) for better performance
                schemas.EnsureCollectionExistsAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                prompts.EnsureCollectionExistsAsync().ConfigureAwait(false).GetAwaiter().GetResult();

                stopwatch.Stop();
                _logger.LogInformation("Created SQLiteVectorStore at {DbPath} in {ElapsedMs}ms",
                    dbPath, stopwatch.ElapsedMilliseconds);

                return new SqlVectorStores(schemas, prompts);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Failed to create SQLiteVectorStore at {DbPath} after {ElapsedMs}ms",
                    dbPath, stopwatch.ElapsedMilliseconds);
                throw;
            }
        }

        /// <summary>
        /// Internal PDF store creation with performance optimizations
        /// </summary>
        private PdfVectorStores CreatePdfStoreInternal(string dbPath)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var dir = Path.GetDirectoryName(dbPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                // Use optimized connection string
                var connectionString = GetOptimizedConnectionString(dbPath);

                // Configure SQLite for performance on first connection
                ConfigureSqlitePerformance(connectionString);

                var options = new SqliteCollectionOptions
                {
                    EmbeddingGenerator = _embedderpdfNew
                };

                // Create collections
                var chapters = new SqliteCollection<string, PdfChapterRecord>(connectionString, "chapter_index", options);
                var prompts = new SqliteCollection<string, PromptRecord>(connectionString, "prompts", options);

                // Initialize collection - use ConfigureAwait(false) for better performance
                chapters.EnsureCollectionExistsAsync().ConfigureAwait(false).GetAwaiter().GetResult();

                stopwatch.Stop();
                _logger.LogInformation("Created PDF VectorStore at {DbPath} in {ElapsedMs}ms, Exists={Exists}",
                    dbPath, stopwatch.ElapsedMilliseconds, File.Exists(dbPath));

                return new PdfVectorStores(connectionString, chapters, prompts);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Failed to create PDF VectorStore at {DbPath} after {ElapsedMs}ms",
                    dbPath, stopwatch.ElapsedMilliseconds);
                throw;
            }
        }

        /// <summary>
        /// Configure SQLite performance settings on first connection
        /// </summary>
        private void ConfigureSqlitePerformance(string connectionString)
        {
            try
            {
                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                using var command = connection.CreateCommand();

                // Set performance PRAGMAs
                command.CommandText = @"
                    PRAGMA journal_mode = WAL;
                    PRAGMA synchronous = NORMAL;
                    PRAGMA cache_size = -10000;
                    PRAGMA temp_store = MEMORY;
                    PRAGMA busy_timeout = 3000;
                ";

                command.ExecuteNonQuery();

                _logger.LogDebug("Configured SQLite performance settings");
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not configure SQLite performance settings");
                // Continue without optimizations
            }
        }

        /// <summary>
        /// Optional: Pre-warm caches for faster first access
        /// </summary>
        public void PreloadStores(IEnumerable<string> sqlDbNames, IEnumerable<string> pdfDbNames)
        {
            var tasks = new List<Task>();

            foreach (var dbName in sqlDbNames)
            {
                var dbPath = Path.Combine(AppContext.BaseDirectory, dbName);
                if (!_sqlStores.ContainsKey(dbPath))
                {
                    tasks.Add(Task.Run(() => CreateOptimizedSqlStore(dbPath)));
                }
            }

            foreach (var dbName in pdfDbNames)
            {
                var dbPath = Path.Combine(AppContext.BaseDirectory, dbName);
                if (!_pdfStores.ContainsKey(dbPath))
                {
                    tasks.Add(Task.Run(() => CreateOptimizedPdfStore(dbPath)));
                }
            }

            if (tasks.Count > 0)
            {
                Task.WhenAll(tasks).ConfigureAwait(false);
                _logger.LogInformation("Preloaded {Count} vector stores", tasks.Count);
            }
        }

        /// <summary>
        /// Clear specific cache entry (useful for testing)
        /// </summary>
        public void ClearCache(string dbName, bool isPdf = false)
        {
            var dbPath = Path.Combine(AppContext.BaseDirectory, dbName);

            if (isPdf)
            {
                _pdfStores.TryRemove(dbPath, out _);
            }
            else
            {
                _sqlStores.TryRemove(dbPath, out _);
            }

            _logger.LogDebug("Cleared cache for {DbPath} (PDF: {IsPdf})", dbPath, isPdf);
        }
    }
}

#endregion

