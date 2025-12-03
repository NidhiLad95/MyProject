using BOL;
using GenxAi_Solutions_V1.Dtos;
using GenxAi_Solutions_V1.Models;
using GenxAi_Solutions_V1.Services.Interfaces;
using GenxAi_Solutions_V1.Utils;
using ImageMagick;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.SqliteVec;
using Microsoft.SemanticKernel.Embeddings;
using MySqlX.XDevAPI;
using Newtonsoft.Json;
using Org.BouncyCastle.Utilities.Collections;
using System.IO;
using System.Text;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;
using Kernel = Microsoft.SemanticKernel.Kernel;

namespace GenxAi_Solutions_V1.Services
{

    public class VectorStoreSeedService : IVectorStoreSeedService
    {
       // private readonly Kernel _kernel;
       // private readonly ITextEmbeddingGenerationService _embedder;
       // private readonly IEmbeddingGenerator<string, Embedding<float>> _pdfembedder;
        private readonly IVectorStoreFactory _storeFactory;
        private readonly ILogger<VectorStoreSeedService> _log;
        private readonly IAuditLogger _audit;
       // private readonly SqlVectorStores _stores;//NEW
        private readonly IEmbeddingGenerator<string, Embedding<float>> _embedderNew;     // for SQL schemas / generic docs
        private readonly IEmbeddingGenerator<string, Embedding<float>> _pdfembedderNew; // for PDF RAG
        public VectorStoreSeedService(/*Kernel kernel,*/ IConfiguration config, IVectorStoreFactory storeFactory, ILogger<VectorStoreSeedService> log, IAuditLogger audit,
            IEmbeddingGenerator<string, Embedding<float>> embeddernew,
        IEmbeddingGenerator<string, Embedding<float>> pdfembeddernew//, SqlVectorStores stores
            )
        {
           // _kernel = kernel;

            // Use the same serviceId you registered in DepandancyInjectionRegister
            var serviceId = config["OpenAI:EmbederServiceId"];

            // Option 1 (Semantic Kernel helper): resolves by serviceId from the kernel’s internal container
           // _embedder = _kernel.GetRequiredService<ITextEmbeddingGenerationService>(serviceId);
            _storeFactory = storeFactory;
            _embedderNew = embeddernew;
            _pdfembedderNew = pdfembeddernew;
            // Option 2 (keyed DI extension) if you prefer:
            // _embedder = _kernel.Services.GetRequiredKeyedService<ITextEmbeddingGenerationService>(serviceId);
           // _pdfembedder = _kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
            _log = log; _audit = audit;
           // _stores = stores;
        }

        #region Kernel

        //public async Task SeedAsync(SQLiteVectorStore store, string connectionString, string? schemaName, string? TablesSelected, string ViewsSelected, CancellationToken ct)
        //{
        //    _log.LogInformation("SeedAsync start db={Db} tablesSel={TablesSel} viewsSel={ViewsSel}", schemaName, TablesSelected, ViewsSelected);
        //    _audit.LogGeneralAudit("Seed.SQL.Details", "system", "-", $"db={schemaName}");

        //    var dict = DatabaseReading.GetSchemaDict_Up(connectionString, schemaName,
        //        (!String.IsNullOrEmpty(TablesSelected)) ? (TablesSelected.Split(',')).ToList<string>() : new List<string>(),
        //        (!String.IsNullOrEmpty(ViewsSelected)) ? (ViewsSelected.Split(',')).ToList<string>() : new List<string>());

        //    foreach (var kv in dict)
        //    {
        //        ct.ThrowIfCancellationRequested();

        //        var tableName = kv.Key;
        //        var schemaJson = JsonConvert.SerializeObject(kv.Value);

        //        // SK returns Embedding<float>; ToArray() gives float[]
        //        var embedding = (await _embedder.GenerateEmbeddingAsync(schemaJson)).ToArray();

        //        // adapt to your ctor / method names
        //        var entry = new BOL.SchemaEntry(tableName, schemaJson, embedding);

        //        store.AddTable(entry); // or store.AddTable(entry);
        //    }

        //    // ---- 2) Seed COLUMNS (per table) ----
        //    foreach (var t in dict)
        //    {
        //        var tableName = t.Key;
        //        // Skip if already seeded
        //        if (store.GetColumnsForTable(tableName).Any()) continue;

        //        var colsRaw = t.Value; // List<Dictionary<string,string>> (expects key "name")
        //        var batch = new List<ColumnEntry>();

        //        foreach (var colDict in colsRaw)
        //        {
        //            if (!colDict.TryGetValue("name", out var colName) || string.IsNullOrWhiteSpace(colName))
        //                continue;

        //            if (!colDict.TryGetValue("type", out var colType) || string.IsNullOrWhiteSpace(colType))
        //                continue;

        //            // Richer text helps semantics
        //            var textForEmb = $"Column {colName.Replace("_", " ")} with Type {colType} in table {tableName.Replace("_", " ")}";
        //            //var colEmb = embedder.GenerateEmbeddingAsync(textForEmb).GetAwaiter().GetResult().ToArray();
        //            var colEmb = (await _embedder.GenerateEmbeddingAsync(textForEmb)).ToArray();

        //            batch.Add(new ColumnEntry
        //            {
        //                TableName = tableName,
        //                ColumnName = colName,
        //                ColumnType = colType,
        //                AliasesCsv = colName.Replace("_", " "),
        //                Embedding = colEmb
        //            });
        //        }

        //        if (batch.Count > 0)
        //            store.UpsertColumns(batch);
        //    }

        //    _log.LogInformation("SeedAsync done db={Db}", schemaName);
        //}

        //public async Task SeedPromptsAsync(SQLiteVectorStore store, string promptstr, CancellationToken ct)
        //{
        //    _log.LogInformation("SeedPromptsAsync start");
        //    //Currently not needed // Nidhi
        //    //var prompts = PromptService.BuildSqlPrompt(promptstr);
        //    foreach (var (name, prompt) in new Dictionary<string, string> { { "Default", promptstr } })
        //    {

        //        ct.ThrowIfCancellationRequested();
        //        var emb = (await _embedder.GenerateEmbeddingAsync(prompt)).ToArray();
        //        store.UpsertPrompts(new[]
        //        {
        //            new PromptEntries
        //            {
        //                Title     = name,
        //                Text      = prompt,
        //                TagsCsv   = null,
        //                Embedding = emb
        //            }
        //        }); // adapt to your API
        //    }
        //    _log.LogInformation("SeedPromptsAsync done");
        //}

        //public async Task SeedPdfPromptsAsync(string sqliteDbPath, Dictionary<string, string> pdfprompt, CancellationToken ct)
        //{
        //    var store = _storeFactory.Create(sqliteDbPath);

        //    var prompts = pdfprompt;
        //    foreach (var (name, prompt) in prompts)
        //    {
        //        ct.ThrowIfCancellationRequested();
        //        var emb = (await _embedder.GenerateEmbeddingAsync(prompt)).ToArray();
        //        store.UpsertPrompts(new[]
        //        {
        //            new PromptEntries
        //            {
        //                Title     = name,
        //                Text      = prompt,
        //                TagsCsv   = null,
        //                Embedding = emb
        //            }
        //        }); // adapt to your API
        //    }
        //}

        //public async Task<int> SeedDocumentsFromPdfAsync(
        //string sqliteDbPath,
        //IEnumerable<string> filePaths,
        //int sectionMaxWords = 800,
        //int chunkMaxWords = 200,
        //CancellationToken ct = default)
        //{
        //    if (filePaths == null) return 0;

        //    var store = _storeFactory.Create(sqliteDbPath);
        //    int total = 0;
        //    var collectionPrefix = "book_";

        //    foreach (var path in filePaths)
        //    {
        //        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));
        //        if (!System.IO.File.Exists(path)) throw new System.IO.FileNotFoundException("PDF not found", path);

        //        string pdfName = System.IO.Path.GetFileNameWithoutExtension(path);
        //        string collectionName = $"{collectionPrefix}{pdfName}";
        //        ct.ThrowIfCancellationRequested();



        //        // 1) Extract text (PdfPig first; OCR fallback via Tesseract/Magick)
        //        string text = GenxAi_Solutions_V1.Utils.TextExtractor.ExtractTextFromPdf(path); // :contentReference[oaicite:2]{index=2}

        //        // 2) Split into sections/chunks (your util)
        //        var sections = GenxAi_Solutions_V1.Utils.TextExtractor.SplitIntoSections(text, sectionMaxWords); // :contentReference[oaicite:3]{index=3}

        //        int sectionIdx = 0;
        //        foreach (var section in sections)
        //        {
        //            var chunks = GenxAi_Solutions_V1.Utils.TextExtractor.ChunkText(section, chunkMaxWords); // :contentReference[oaicite:4]{index=4}

        //            int chunkIdx = 0;
        //            var toWrite = new List<DocumentChunkEntry>();

        //            foreach (var chunk in chunks)
        //            {
        //                ct.ThrowIfCancellationRequested();

        //                var emb = (await _embedder.GenerateEmbeddingAsync(chunk)).ToArray();
        //                toWrite.Add(new DocumentChunkEntry
        //                {
        //                    DocId = Path.GetFileName(path),
        //                    SectionIndex = sectionIdx,
        //                    ChunkIndex = chunkIdx++,
        //                    Text = chunk,
        //                    Embedding = emb
        //                });
        //            }

        //            if (toWrite.Count > 0)
        //            {
        //                store.UpsertDocumentChunks(toWrite);
        //                total += toWrite.Count;
        //            }

        //            sectionIdx++;
        //        }
        //    }

        //    return total;
        //}

        //public async Task<int> SeedDocumentsFromTextAsync(
        //string sqliteDbPath,
        //string docId,
        //string text,
        //int sectionMaxWords = 800,
        //int chunkMaxWords = 200,
        //CancellationToken ct = default)
        //{
        //    var store = _storeFactory.Create(sqliteDbPath);
        //    int total = 0;

        //    var sections = GenxAi_Solutions_V1.Utils.TextExtractor.SplitIntoSections(text, sectionMaxWords); // :contentReference[oaicite:5]{index=5}

        //    int sectionIdx = 0;
        //    foreach (var section in sections)
        //    {
        //        var chunks = GenxAi_Solutions_V1.Utils.TextExtractor.ChunkText(section, chunkMaxWords); // :contentReference[oaicite:6]{index=6}

        //        int chunkIdx = 0;
        //        var toWrite = new List<DocumentChunkEntry>();

        //        foreach (var chunk in chunks)
        //        {
        //            ct.ThrowIfCancellationRequested();

        //            var emb = (await _embedder.GenerateEmbeddingAsync(chunk)).ToArray();
        //            toWrite.Add(new DocumentChunkEntry
        //            {
        //                DocId = docId,
        //                SectionIndex = sectionIdx,
        //                ChunkIndex = chunkIdx++,
        //                Text = chunk,
        //                Embedding = emb
        //            });
        //        }

        //        if (toWrite.Count > 0)
        //        {
        //            store.UpsertDocumentChunks(toWrite);
        //            total += toWrite.Count;
        //        }

        //        sectionIdx++;
        //    }

        //    return total;
        //}

        //public async Task<IEnumerable<(string Text, double Score)>> SearchDocsAsync(
        //string sqliteDbPath, string query, int topK = 5, string? docIdFilter = null, CancellationToken ct = default)
        //{
        //    var store = _storeFactory.Create(sqliteDbPath);
        //    var q = (await _embedder.GenerateEmbeddingAsync(query)).ToArray();

        //    var hits = store.GetNearestDocumentChunks(q, topK, docIdFilter);
        //    return hits.Select(h => (h.Entry.Text, h.Score));
        //}

        ///// <summary>
        ///// Ingests a single PDF file into the vector DB.
        ///// Creates (if missing) a chapter_index collection plus a per-book collection named {collectionPrefix}{pdfName}.
        ///// Returns UploadResponse with the file name, collection name and number of sections stored.
        ///// </summary>
        //public async Task<UploadResponse> IngestPdfAsync(string filePath, string sqliteDbPath, string collectionPrefix = "book_")
        //{
        //    var _store = _storeFactory.PdfCreate(sqliteDbPath);
        //    if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));
        //    if (!System.IO.File.Exists(filePath)) throw new System.IO.FileNotFoundException("PDF not found", filePath);

        //    string pdfName = System.IO.Path.GetFileNameWithoutExtension(filePath);
        //    string collectionName = $"{collectionPrefix}{pdfName}";


        //    // ensure book collection exists
        //    var bookCollection = _store.GetCollection<string, VectorDBChunksRecord>(collectionName);
        //    if (!await bookCollection.CollectionExistsAsync())
        //        await bookCollection.EnsureCollectionExistsAsync();

        //    // Change this line:
        //    // var chapterIndex = _store.GetCollection<string, VectorDBChunksRecord>("chapter_index");
        //    // to:
        //    var chapterIndex = _store.GetCollection<string, VectorDBSectionRecord>("chapter_index");
        //    if (!await chapterIndex.CollectionExistsAsync())
        //        await chapterIndex.EnsureCollectionExistsAsync();

        //    // extract text (PdfPig or OCR)
        //    string fullText = TextExtractor.ExtractTextFromPdf(filePath);

        //    // split into large sections
        //    var largeSections = TextExtractor.SplitIntoSections(fullText, 4000).ToList();

        //    int sectionsStored = 0;

        //    foreach (var section in largeSections)
        //    {
        //        try
        //        {
        //            sectionsStored++;

        //            // generate embedding for the section (stage 1)
        //            var sectionEmbedding = await RetryAsync(() => _pdfembedder.GenerateAsync(section));
        //            await chapterIndex.UpsertAsync(new VectorDBSectionRecord
        //            {
        //                Key = Guid.NewGuid().ToString("N"),
        //                Embedding = sectionEmbedding.Vector,
        //                Source = pdfName
        //            });

        //            // split into smaller chunks and embed them
        //            var chunks = TextExtractor.ChunkText(section, 250).ToList();
        //            var chunkEmbeddings = await Task.WhenAll(chunks.Select(c => RetryAsync(() => _pdfembedder.GenerateAsync(c))));

        //            // insert chunks sequentially (to avoid write conflicts)
        //            for (int i = 0; i < chunks.Count; i++)
        //            {
        //                await bookCollection.UpsertAsync(new VectorDBChunksRecord
        //                {
        //                    Key = Guid.NewGuid().ToString("N"),
        //                    Embedding = chunkEmbeddings[i].Vector,
        //                    Text = chunks[i],
        //                    Source = pdfName
        //                });
        //            }

        //            //_logger.LogInformation("Ingested section {Section} for {Pdf}. Chunks: {ChunkCount}", sectionsStored, pdfName, chunks.Count);
        //        }
        //        catch (Exception ex)
        //        {
        //            // _logger.LogWarning(ex, "Failed to process a section from {Pdf}: {Message}", pdfName, ex.Message);
        //            // continue with other sections
        //        }
        //    }

        //    return new UploadResponse(System.IO.Path.GetFileName(filePath), collectionName, sectionsStored);
        //}

        //// Fix for CS1513 and CS8600 in QueryAsync method
        //public async Task<pdfQueryResponse> QueryAsync(string question, string sqliteDbPath, int topSections = 30, int topChunks = 5)
        //{
        //    var _store = _storeFactory.PdfCreate(sqliteDbPath);
        //    if (string.IsNullOrWhiteSpace(question)) throw new ArgumentNullException(nameof(question));

        //    var chapterIndex = _store.GetCollection<string, VectorDBSectionRecord>("chapter_index");

        //    // embed question
        //    var qEmbedding = await _pdfembedder.GenerateAsync(question);

        //    // search sections
        //    var sectionResults = chapterIndex.SearchAsync(qEmbedding.Vector, top: topSections);

        //    // accumulate best score per book
        //    var bookScores = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        //    await foreach (var r in sectionResults)
        //    {
        //        if (!r.Score.HasValue) continue;
        //        float score = (float)r.Score.Value;
        //        string book = r.Record.Source;
        //        if (!bookScores.ContainsKey(book) || score > bookScores[book])
        //            bookScores[book] = score;
        //    }

        //    // pick top N books
        //    var topBooks = bookScores.OrderByDescending(kv => kv.Value).Take(5).Select(kv => kv.Key).ToList();

        //    // gather chunk-level context from top books
        //    var contextBuilder = new StringBuilder();
        //    foreach (var book in topBooks)
        //    {
        //        try
        //        {
        //            var bookCollection = _store.GetCollection<string, VectorDBChunksRecord>($"book_{book}");
        //            var chunkResults = bookCollection.SearchAsync(qEmbedding.Vector, top: topChunks);

        //            await foreach (var chunk in chunkResults)
        //            {
        //                contextBuilder.AppendLine($"[Book: {chunk.Record.Source}] {chunk.Record.Text}");
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            // _logger.LogWarning(ex, "Failed to search book collection {Book}", book);
        //        }
        //    }

        //    string context = contextBuilder.ToString();

        //    //// Prepare prompt. You can replace this with your detailed ReleaseOrder prompt.
        //    //string prompt = $"Answer based on the following context:\n{context}\n\nQuestion: {question}";

        //    //// Ask chat model
        //    //string assistantReply = string.Empty;
        //    //try
        //    //{
        //    //    var chatMessage = await _chat.GetChatMessageContentAsync(prompt);
        //    //    assistantReply = chatMessage.Content ?? string.Empty;
        //    //}
        //    //catch (Exception ex)
        //    //{
        //    //    //_logger.LogError(ex, "Chat model call failed: {Message}", ex.Message);
        //    //    // return partial result (context) with error note in reply
        //    //    assistantReply = $"(model call failed: {ex.Message})";
        //    //}

        //    return new pdfQueryResponse(context, topBooks);
        //}

        ///// <summary>
        ///// Retry helper with exponential backoff for transient failures.
        ///// </summary>
        //private static async Task<T> RetryAsync<T>(Func<Task<T>> action, int maxRetries = 3, int initialDelayMs = 1000)
        //{
        //    int delay = initialDelayMs;
        //    for (int attempt = 1; attempt <= maxRetries; attempt++)
        //    {
        //        try
        //        {
        //            return await action();
        //        }
        //        catch (Exception) when (attempt < maxRetries)
        //        {
        //            await Task.Delay(delay);
        //            delay *= 2;
        //        }
        //    }

        //    // final attempt - let exceptions bubble
        //    return await action();
        //}

        //public void Dispose()
        //{
        //    try
        //    {
        //        // Kernel does not implement IDisposable, so just check for IDisposable via Services
        //        if (_kernel.Services is IDisposable d)
        //            d.Dispose();
        //    }
        //    catch (Exception ex)
        //    {
        //        //_logger.LogWarning(ex, "Exception while disposing kernel: {Message}", ex.Message);
        //    }
        //}

        #endregion


        private static string Slugify(string s)
            => System.Text.RegularExpressions.Regex
                .Replace(s.ToLowerInvariant(), @"[^a-z0-9]+", "_")
                .Trim('_');

        public async Task SeedAsyncNew(SqlVectorStores store, string connectionString, string? schemaName, string? TablesSelected, string ViewsSelected, CancellationToken ct)
        {
            _log.LogInformation("SeedAsync start db={Db} tablesSel={TablesSel} viewsSel={ViewsSel}", schemaName, TablesSelected, ViewsSelected);
            _audit.LogGeneralAudit("Seed.SQL.Details", "system", "-", $"db={schemaName}");
            var dict = DatabaseReading.GetSchemaDict_UpNew(connectionString, schemaName,
                (!String.IsNullOrEmpty(TablesSelected)) ? (TablesSelected.Split(',')).ToList<string>() : new List<string>(),
                (!String.IsNullOrEmpty(ViewsSelected)) ? (ViewsSelected.Split(',')).ToList<string>() : new List<string>());

            foreach (var s in await dict)
            {
                var rec = new SchemaRecord
                {
                    Id = s.TableName,
                    Name = s.TableName,
                    SchemaText = s.SchemaText
                };

                await store.Schemas.UpsertAsync(rec);
            }

            _log.LogInformation("SeedAsync done db={Db}", schemaName);
        }

        public async Task SeedPromptsAsyncNew(SqlVectorStores store, string promptstr, CancellationToken ct)
        {
            _log.LogInformation("SeedPromptsAsync start");

            // Seed rules prompt
            var rules = await store.Prompts.GetAsync("sql_rules");
            if (rules is null)
            {
                await store.Prompts.UpsertAsync(new PromptRecord
                {
                    Id = "sql_rules",
                    Text = promptstr + "/n/n" + PromptService.Rules
                });
            }

            await store.Prompts.UpsertAsync(new PromptRecord
            {
                Id = "sql_seeded",
                Text = $"Seeded at {DateTime.UtcNow:O}"
            });


            _log.LogInformation("SeedPromptsAsync done");
        }
        public async Task<UploadResponse> IngestPdfAsyncNew(PdfVectorStores store,string filePath, string sqliteDbPath, string collectionPrefix = "book_")
        {
            var _store = store; //_storeFactory.PdfCreate_New(sqliteDbPath);

            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));
            if (!System.IO.File.Exists(filePath)) throw new System.IO.FileNotFoundException("PDF not found", filePath);

            string pdfName = System.IO.Path.GetFileNameWithoutExtension(filePath);
            string collectionName = $"{collectionPrefix}{pdfName}";
            var slug = Slugify(pdfName);
            var seededKey = $"pdf:{slug}:seeded";

            int sectionsStored = 0;

            // Simple marker using PdfChapterRecord
            var marker = await _store.Chapters.GetAsync(seededKey);
            if (marker is not null) return new UploadResponse(System.IO.Path.GetFileName(filePath), collectionName, sectionsStored);

            //var chapterIndex = _store.Chapters;


            // extract text (PdfPig or OCR)
            string fullText = TextExtractor.ExtractTextFromPdf(filePath);

            // split into large sections
            //var largeSections = TextExtractor.SplitIntoSections(fullText, 4000).ToList();
            var (chapters, chunks) = TextExtractor.Read(filePath, collectionName);

            // 1) common chapter table in pdf_vectors.db
            foreach (var ch in chapters)
            {
                sectionsStored++;
                await _store.Chapters.UpsertAsync(ch);
            }

            var options = new SqliteCollectionOptions { EmbeddingGenerator = _pdfembedderNew };
            var chunksCollection = new SqliteCollection<string, PdfChunkRecord>(
                _store.PdfConnectionString,
               // $"Book_{slug}",    // <-- prefix Book_
               collectionName,
                options);

            await chunksCollection.EnsureCollectionExistsAsync();


            foreach (var ck in chunks)
            {

                await chunksCollection.UpsertAsync(ck);
            }

            // marker record (chapterIndex = -1)
            await _store.Chapters.UpsertAsync(new PdfChapterRecord
            {
                Id = seededKey,
                BookName = collectionName,
                PdfSlug = slug,
                ChapterIndex = -1,
                Text = "seeded marker"
            });





            return new UploadResponse(System.IO.Path.GetFileName(filePath), collectionName, sectionsStored);
        }

        public async Task SeedPdfPromptsAsyncNew(PdfVectorStores store, string promptstr, CancellationToken ct)
        {
            var _store = store;

            _log.LogInformation("SeedPdfPromptsAsyncNew start");

            // Seed rules prompt
            var rules = await store.Prompts.GetAsync("pdf_rules");
            if (rules is null)
            {
                await store.Prompts.UpsertAsync(new PromptRecord
                {
                    Id = "pdf_rules",
                    Text = promptstr + "/n/n" + PromptService.Rules
                });
            }

            await store.Prompts.UpsertAsync(new PromptRecord
            {
                Id = "pdf_rules",
                Text = $"Seeded at {DateTime.UtcNow:O}"
            });


            _log.LogInformation("SeedPdfPromptsAsyncNew done");
        }
    }

}
