using BOL;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;

namespace GenxAi_Solutions.Utils
{
    public class SQLiteVectorStore
    {
        private readonly string _dbPath;
        private readonly ILogger<SQLiteVectorStore> _logger;

        public SQLiteVectorStore(string dbPath, ILogger<SQLiteVectorStore> logger)
        {
            //_dbPath = dbPath;
            _logger = logger;
            // allow absolute, otherwise put under app base
            _dbPath = Path.IsPathRooted(dbPath)
                ? dbPath
                : Path.Combine(AppContext.BaseDirectory, dbPath);

            //// 👇 Add this log line here
            //Console.WriteLine($"[SQLiteVectorStore] Using DB: {_dbPath}  Exists={File.Exists(_dbPath)}");
            _logger.LogInformation("SQLiteVectorStore DB: {Path} Exists={Exists}", _dbPath, File.Exists(_dbPath));
            Initialize();
        }
        
        private void Initialize()
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

           //schema
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS SchemaEntries (
            TableName TEXT,
            SchemaText TEXT,
           Embedding TEXT
            )";

                cmd.ExecuteNonQuery();
            }

            // Columns table
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS ColumnEntries (
    TableName   TEXT,
    ColumnName  TEXT,
    ColumnType  TEXT,
    AliasesCsv  TEXT,
    Embedding   TEXT,
    PRIMARY KEY (TableName, ColumnName,ColumnType)
)";
                cmd.ExecuteNonQuery();
            }

            // Prompts table
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS PromptEntries (
    PromptId    TEXT PRIMARY KEY,
    Title       TEXT,
    Text        TEXT,
    TagsCsv     TEXT,
    Embedding   TEXT
)";
                cmd.ExecuteNonQuery();
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS DocumentChunks (
    DocId        TEXT,
    SectionIndex INTEGER,
    ChunkIndex   INTEGER,
    Text         TEXT,
    Embedding    TEXT,
    PRIMARY KEY (DocId, SectionIndex, ChunkIndex)
)";
                cmd.ExecuteNonQuery();
            }
        }
        

        public void AddTable1(SchemaEntry entry)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
        INSERT INTO SchemaEntries (TableName, SchemaText, Embedding)
        VALUES ($tableName, $schemaText, $embedding)";
            cmd.Parameters.AddWithValue("$tableName", entry.TableName);
            cmd.Parameters.AddWithValue("$schemaText", entry.SchemaText);
            cmd.Parameters.AddWithValue("$embedding", JsonConvert.SerializeObject(entry.Embedding));
            cmd.ExecuteNonQuery();
        }

        public void AddTable(SchemaEntry entry)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            // Check if the table already exists
            var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = @"
        SELECT COUNT(*) FROM SchemaEntries WHERE TableName = $tableName";
            checkCmd.Parameters.AddWithValue("$tableName", entry.TableName);

            long count = (long)checkCmd.ExecuteScalar();

            if (count > 0)
            {
                // Table already exists, so we do nothing
                return;
            }

            // Table doesn't exist, proceed with insertion
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
        INSERT INTO SchemaEntries (TableName, SchemaText, Embedding)
        VALUES ($tableName, $schemaText, $embedding)";
            cmd.Parameters.AddWithValue("$tableName", entry.TableName);
            cmd.Parameters.AddWithValue("$schemaText", entry.SchemaText);
            cmd.Parameters.AddWithValue("$embedding", JsonConvert.SerializeObject(entry.Embedding));
            cmd.ExecuteNonQuery();
        }

        public long CountSchemaRows()
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM SchemaEntries;";
            return (long)(cmd.ExecuteScalar() ?? 0L);
        }
        #region Columns(new logoc)

        // ---------- Columns ----------   
        public void UpsertColumns(IEnumerable<ColumnEntry> cols)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            foreach (var c in cols)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
INSERT INTO ColumnEntries (TableName, ColumnName, ColumnType, AliasesCsv, Embedding)
VALUES ($t, $c, $y, $a, $e)
ON CONFLICT(TableName, ColumnName,ColumnType) DO UPDATE SET
    AliasesCsv = excluded.AliasesCsv,
    Embedding  = excluded.Embedding
";
                cmd.Parameters.AddWithValue("$t", c.TableName);
                cmd.Parameters.AddWithValue("$c", c.ColumnName);
                cmd.Parameters.AddWithValue("$y", c.ColumnType);
                cmd.Parameters.AddWithValue("$a", (object?)c.AliasesCsv ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$e", JsonConvert.SerializeObject(c.Embedding));
                cmd.ExecuteNonQuery();
            }
        }

        public IEnumerable<ColumnEntry> GetColumnsForTable(string tableName)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT TableName, ColumnName, ColumnType, AliasesCsv, Embedding
                                FROM ColumnEntries WHERE TableName = $t";
            cmd.Parameters.AddWithValue("$t", tableName);

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                yield return new ColumnEntry
                {
                    TableName = r.GetString(0),
                    ColumnName = r.GetString(1),
                    ColumnType = r.GetString(2),
                    AliasesCsv = r.IsDBNull(3) ? null : r.GetString(3),
                    Embedding = JsonConvert.DeserializeObject<float[]>(r.GetString(4))!
                };
            }
        }

        //public IEnumerable<(ColumnEntry Entry, double Score)> GetNearestColumns(float[] query, string tableName, int topK)
        //{
        //    return GetColumnsForTable(tableName)
        //        .Select(e => (e, Score: TextSimilarity.Cosine(query, e.Embedding)))
        //        .OrderByDescending(x => x.Score)
        //        .Take(topK);
        //}
        public IEnumerable<(ColumnEntry Entry, double Score)> GetNearestColumns(float[] query, string tableName, int topK)
        {
            return GetColumnsForTable(tableName)
                .Select(e => (e, Score: TextSimilarity.Cosine(query, e.Embedding)))
                .OrderByDescending(x => x.Score);
                //.Take(topK);
        }
        #endregion Columns(new logoc)

        #region Prompts(New logic)
        // ---------- Prompts ----------
        public void UpsertPrompts(IEnumerable<PromptEntries> prompts)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            foreach (var p in prompts)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
INSERT INTO PromptEntries (PromptId, Title, Text, TagsCsv, Embedding)
VALUES ($id, $title, $text, $tags, $emb)
ON CONFLICT(PromptId) DO UPDATE SET
    Title = excluded.Title,
    Text  = excluded.Text,
    TagsCsv = excluded.TagsCsv,
    Embedding = excluded.Embedding
";
                cmd.Parameters.AddWithValue("$id", p.PromptId);
                cmd.Parameters.AddWithValue("$title", p.Title);
                cmd.Parameters.AddWithValue("$text", p.Text);
                cmd.Parameters.AddWithValue("$tags", (object?)p.TagsCsv ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$emb", JsonConvert.SerializeObject(p.Embedding));
                cmd.ExecuteNonQuery();
            }
        }

        public IEnumerable<PromptEntries> GetAllPrompts()
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT PromptId, Title, Text, TagsCsv, Embedding FROM PromptEntries";

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                yield return new PromptEntries
                {
                    PromptId = r.GetString(0),
                    Title = r.GetString(1),
                    Text = r.GetString(2),
                    TagsCsv = r.IsDBNull(3) ? null : r.GetString(3),
                    Embedding = JsonConvert.DeserializeObject<float[]>(r.GetString(4))!
                };
            }
        }

        public IEnumerable<(PromptEntries Entry, double Score)> GetNearestPrompts(float[] query, int topK, string? tagFilter = null)
        {
            var all = GetAllPrompts();

            if (!string.IsNullOrWhiteSpace(tagFilter))
            {
                var tag = tagFilter.Trim().ToLowerInvariant();
                all = all.Where(p => (p.TagsCsv ?? "")
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Any(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase)));
            }

            return all
                .Select(p => (p, Score: TextSimilarity.Cosine(query, p.Embedding)))
                .OrderByDescending(x => x.Score)
                .Take(topK);
        }

        #endregion Prompts(New logic)

        #region Old Logic
        public void AddPrompts(PromptEntry entry)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            var cmdremv = conn.CreateCommand();
            cmdremv.CommandText = "DELETE FROM PromptEntries";
            cmdremv.ExecuteNonQuery();

            var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = @"
        SELECT COUNT(*) FROM PromptEntries WHERE PromptText = $promtText";
            checkCmd.Parameters.AddWithValue("$promtText", entry.PromptText);

            long count = (long)checkCmd.ExecuteScalar();

            if (count > 0)
            {
                // Table already exists, so we do nothing
                return;
            }


            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
        INSERT INTO PromptEntries (PromptName, PromptText, Embedding)
        VALUES ($promptName, $promptText, $embedding)";
            cmd.Parameters.AddWithValue("$promptName", entry.PromptName);
            cmd.Parameters.AddWithValue("$promptText", entry.PromptText);
            cmd.Parameters.AddWithValue("$embedding", JsonConvert.SerializeObject(entry.Embedding));
            cmd.ExecuteNonQuery();
        }

        public IEnumerable<SchemaEntry> GetAlltables_old()
        {
            
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT TableName, SchemaText, Embedding FROM SchemaEntries";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string tableName = reader.GetString(0);
                string schemaText = reader.GetString(1);
                float[] embedding = JsonConvert.DeserializeObject<float[]>(reader.GetString(2));

                yield return new SchemaEntry(tableName, schemaText, embedding);
            }
        }

        public List<SchemaEntry> GetAllTables()
        {
            var results = new List<SchemaEntry>();

            try
            {
                using var conn = new SqliteConnection($"Data Source={_dbPath}");
                conn.Open();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT TableName, SchemaText, Embedding FROM SchemaEntries";

                using var reader = cmd.ExecuteReader();

                // ordinals once
                int oName = reader.GetOrdinal("TableName");
                int oText = reader.GetOrdinal("SchemaText");
                int oEmb = reader.GetOrdinal("Embedding");

                while (reader.Read())
                {
                    if (reader.IsDBNull(oName)) continue;
                    var tableName = reader.GetString(oName);
                    if (string.IsNullOrWhiteSpace(tableName)) continue;

                    var schemaText = reader.IsDBNull(oText) ? "" : reader.GetString(oText);

                    // Embedding stored as JSON text
                    float[] embedding;
                    if (reader.IsDBNull(oEmb))
                    {
                        embedding = Array.Empty<float>();
                    }
                    else
                    {
                        var embJson = reader.GetString(oEmb);
                        try
                        {
                            embedding = JsonConvert.DeserializeObject<float[]>(embJson) ?? Array.Empty<float>();
                        }
                        catch
                        {
                            embedding = Array.Empty<float>(); // bad JSON -> skip vector but keep row
                        }
                    }

                    results.Add(new SchemaEntry(tableName, schemaText, embedding));
                }
            }
            catch
            {
                // optional: log; but don't return null
                return new List<SchemaEntry>();
            }

            return results; // never null; possibly empty
        }

        public void UpsertDocumentChunks(IEnumerable<DocumentChunkEntry> chunks)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            foreach (var c in chunks)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
INSERT INTO DocumentChunks (DocId, SectionIndex, ChunkIndex, Text, Embedding)
VALUES ($d, $s, $c, $txt, $emb)
ON CONFLICT(DocId, SectionIndex, ChunkIndex) DO UPDATE SET
    Text = excluded.Text,
    Embedding = excluded.Embedding;";
                cmd.Parameters.AddWithValue("$d", c.DocId);
                cmd.Parameters.AddWithValue("$s", c.SectionIndex);
                cmd.Parameters.AddWithValue("$c", c.ChunkIndex);
                cmd.Parameters.AddWithValue("$txt", c.Text);
                cmd.Parameters.AddWithValue("$emb", JsonConvert.SerializeObject(c.Embedding));
                cmd.ExecuteNonQuery();
            }
        }

        // Vector search over document chunks
        public IEnumerable<(DocumentChunkEntry Entry, double Score)> GetNearestDocumentChunks(float[] query, int topK, string? docIdFilter = null)
        {
            var rows = new List<DocumentChunkEntry>();

            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = string.IsNullOrWhiteSpace(docIdFilter)
                ? @"SELECT DocId, SectionIndex, ChunkIndex, Text, Embedding FROM DocumentChunks"
                : @"SELECT DocId, SectionIndex, ChunkIndex, Text, Embedding FROM DocumentChunks WHERE DocId = $d";

            if (!string.IsNullOrWhiteSpace(docIdFilter))
                cmd.Parameters.AddWithValue("$d", docIdFilter);

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                rows.Add(new DocumentChunkEntry
                {
                    DocId = r.GetString(0),
                    SectionIndex = r.GetInt32(1),
                    ChunkIndex = r.GetInt32(2),
                    Text = r.GetString(3),
                    Embedding = JsonConvert.DeserializeObject<float[]>(r.GetString(4))!
                });
            }

            return rows
                .Select(x => (x, Score: GenxAi_Solutions.Utils.TextSimilarity.Cosine(query, x.Embedding)))
                .OrderByDescending(x => x.Score)
                .Take(topK);
        }

        public IEnumerable<SchemaEntry> GetNearest(float[] query, int topK)
        {
            
            return GetAlltables_old()
                .Select(e => new { Entry = e, Score = CosineSimilarity(query, e.Embedding) })
                .OrderByDescending(x => x.Score)
                .Take(topK)
                .Select(x => x.Entry);
        }

        public IReadOnlyList<SchemaEntry> GetNearestTable(float[] query, int topK)
        {
            if (query == null || query.Length == 0 || topK <= 0)
                return Array.Empty<SchemaEntry>();

            // Make sure we have a non-null, materialized set
            var all = (GetAllTables() ?? Enumerable.Empty<SchemaEntry>()).ToList();
            if (all.Count == 0) return all; // empty list, not null

            var ranked = new List<(SchemaEntry Entry, double Score)>(capacity: all.Count);

            foreach (var e in all)
            {
                if (e == null || e.Embedding == null) continue;
                if (e.Embedding.Length != query.Length) continue; // model/dimension mismatch

                var score = TextSimilarity.Cosine(query, e.Embedding); // or your CosineSimilarity
                if (double.IsNaN(score) || double.IsInfinity(score)) continue;

                ranked.Add((e, score));
            }

            return ranked
                .OrderByDescending(x => x.Score)
                .Take(topK)
                .Select(x => x.Entry)
                .ToList();  // materialize to avoid deferred-exec surprises
        }

        private static float CosineSimilarity(float[] v1, float[] v2)
        {
            double dot = 0, norm1 = 0, norm2 = 0;
            for (int i = 0; i < v1.Length; i++)
            {
                dot += v1[i] * v2[i];
                norm1 += v1[i] * v1[i];
                norm2 += v2[i] * v2[i];
            }
            return (float)(dot / (Math.Sqrt(norm1) * Math.Sqrt(norm2) + 1e-8));
        }
        #endregion Old Logic
    }
}
