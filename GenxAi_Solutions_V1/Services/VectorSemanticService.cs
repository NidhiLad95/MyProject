using GenxAi_Solutions_V1.Dtos;
using GenxAi_Solutions_V1.Models;
using GenxAi_Solutions_V1.Services.Interfaces;
using GenxAi_Solutions_V1.Utils;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.SqliteVec;
using System.Text;
using SKKernel = Microsoft.SemanticKernel.Kernel;

namespace GenxAi_Solutions_V1.Services
{/// <summary>
 /// VectorSemanticService
 /// - Builds Semantic Kernel with OpenAI chat + embedding + sqlite vector store
 /// - Offers methods to ingest PDFs and query the vector DB
 /// </summary>
 /// 
    public class VectorSemanticService : IVectorSemanticService
    {
        private readonly ILogger<VectorSemanticService> _logger;
        private readonly SKKernel _kernel;
        private readonly IEmbeddingGenerator<string, Embedding<float>> _embedder;
        private readonly SqliteVectorStore _store;
        private readonly IChatCompletionService _chat;
        private readonly IVectorStoreFactory _storeFactory;

        // you can change these model names via code if you like
        private const string ChatModel = "gpt-5-mini";
        private const string EmbeddingModel = "text-embedding-3-small";

        
    

        public VectorSemanticService(IConfiguration config, ILogger<VectorSemanticService> logger, IVectorStoreFactory storeFactory, ChatClientAgent pdfAgent, IChatHistoryService history)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            string openAIApiKey = config["OpenAI:ApiKey"]
                ?? throw new ArgumentNullException("OpenAI:ApiKey", "OpenAI API key is required in configuration.");

            string dbPath = config["Sqlite:DbPath"] ?? "Data Source=textbook.db";


            // Add an explicit using alias for Microsoft.SemanticKernel.Kernel at the top of the file


            // To:
            var builder = SKKernel.CreateBuilder();
            // chat completion
            builder.Services.AddOpenAIChatCompletion(ChatModel, openAIApiKey);
#pragma warning disable SKEXP0010
            // embedding generator
            builder.Services.AddOpenAIEmbeddingGenerator(EmbeddingModel, openAIApiKey);
#pragma warning restore SKEXP0010
            // sqlite vector store (SqliteVectorStore expects a factory returning connection string)
            builder.Services.AddSqliteVectorStore(_ => dbPath);

            _kernel = builder.Build();

            // Resolve services
            _embedder = _kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
            _store = _kernel.Services.GetRequiredService<SqliteVectorStore>();
            _chat = _kernel.GetRequiredService<IChatCompletionService>();
            //_pdfAgent = pdfAgent;
            //_history = history;
            storeFactory = storeFactory;
        }

        /// <summary>
        /// Ingests a single PDF file into the vector DB.
        /// Creates (if missing) a chapter_index collection plus a per-book collection named {collectionPrefix}{pdfName}.
        /// Returns UploadResponse with the file name, collection name and number of sections stored.
        /// </summary>
        public async Task<UploadResponse> IngestPdfAsync(string filePath, string collectionPrefix = "book_")
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));
            if (!System.IO.File.Exists(filePath)) throw new System.IO.FileNotFoundException("PDF not found", filePath);

            string pdfName = System.IO.Path.GetFileNameWithoutExtension(filePath);
            string collectionName = $"{collectionPrefix}{pdfName}";

            // ensure book collection exists
            var bookCollection = _store.GetCollection<string, VectorDBChunksRecord>(collectionName);
            if (!await bookCollection.CollectionExistsAsync())
                await bookCollection.EnsureCollectionExistsAsync();

            // Change this line:
            // var chapterIndex = _store.GetCollection<string, VectorDBChunksRecord>("chapter_index");
            // to:
            var chapterIndex = _store.GetCollection<string, VectorDBSectionRecord>("chapter_index");
            if (!await chapterIndex.CollectionExistsAsync())
                await chapterIndex.EnsureCollectionExistsAsync();

            // extract text (PdfPig or OCR)
            string fullText = TextExtractor.ExtractTextFromPdf(filePath);

            // split into large sections
            var largeSections = TextExtractor.SplitIntoSections(fullText, 4000).ToList();

            int sectionsStored = 0;

            foreach (var section in largeSections)
            {
                try
                {
                    sectionsStored++;

                    // generate embedding for the section (stage 1)
                    var sectionEmbedding = await RetryAsync(() => _embedder.GenerateAsync(section));
                    await chapterIndex.UpsertAsync(new VectorDBSectionRecord
                    {
                        Key = Guid.NewGuid().ToString("N"),
                        Embedding = sectionEmbedding.Vector,
                        Source = pdfName
                    });

                    // split into smaller chunks and embed them
                    var chunks = TextExtractor.ChunkText(section, 250).ToList();
                    var chunkEmbeddings = await Task.WhenAll(chunks.Select(c => RetryAsync(() => _embedder.GenerateAsync(c))));

                    // insert chunks sequentially (to avoid write conflicts)
                    for (int i = 0; i < chunks.Count; i++)
                    {
                        await bookCollection.UpsertAsync(new VectorDBChunksRecord
                        {
                            Key = Guid.NewGuid().ToString("N"),
                            Embedding = chunkEmbeddings[i].Vector,
                            Text = chunks[i],
                            Source = pdfName
                        });
                    }

                    _logger.LogInformation("Ingested section {Section} for {Pdf}. Chunks: {ChunkCount}", sectionsStored, pdfName, chunks.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process a section from {Pdf}: {Message}", pdfName, ex.Message);
                    // continue with other sections
                }
            }

            return new UploadResponse(System.IO.Path.GetFileName(filePath), collectionName, sectionsStored);
        }

       
        // Fix for CS1513 and CS8600 in QueryAsync method
        public async Task<QueryResponse> QueryAsync(string question, int topSections = 30, int topChunks = 5)
        {
            if (string.IsNullOrWhiteSpace(question)) throw new ArgumentNullException(nameof(question));

            var chapterIndex = _store.GetCollection<string, VectorDBSectionRecord>("chapter_index");

            // embed question
            var qEmbedding = await _embedder.GenerateAsync(question);

            // search sections
            var sectionResults = chapterIndex.SearchAsync(qEmbedding.Vector, top: topSections);

            // accumulate best score per book
            var bookScores = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            await foreach (var r in sectionResults)
            {
                if (!r.Score.HasValue) continue;
                float score = (float)r.Score.Value;
                string book = r.Record.Source;
                if (!bookScores.ContainsKey(book) || score > bookScores[book])
                    bookScores[book] = score;
            }

            // pick top N books
            var topBooks = bookScores.OrderByDescending(kv => kv.Value).Take(5).Select(kv => kv.Key).ToList();

            // gather chunk-level context from top books
            var contextBuilder = new StringBuilder();
            foreach (var book in topBooks)
            {
                try
                {
                    var bookCollection = _store.GetCollection<string, VectorDBChunksRecord>($"book_{book}");
                    var chunkResults = bookCollection.SearchAsync(qEmbedding.Vector, top: topChunks);

                    await foreach (var chunk in chunkResults)
                    {
                        contextBuilder.AppendLine($"[Book: {chunk.Record.Source}] {chunk.Record.Text}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to search book collection {Book}", book);
                }
            }

            string context = contextBuilder.ToString();

            // Prepare prompt. You can replace this with your detailed ReleaseOrder prompt.
            string prompt = $"Answer based on the following context:\n{context}\n\nQuestion: {question}";

            // Ask chat model
            string assistantReply = string.Empty;
            try
            {
                var chatMessage = await _chat.GetChatMessageContentAsync(prompt);
                assistantReply = chatMessage.Content ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chat model call failed: {Message}", ex.Message);
                // return partial result (context) with error note in reply
                assistantReply = $"(model call failed: {ex.Message})";
            }

            return new QueryResponse(assistantReply, context, topBooks);
        }

        /// <summary>
        /// Retry helper with exponential backoff for transient failures.
        /// </summary>
        private static async Task<T> RetryAsync<T>(Func<Task<T>> action, int maxRetries = 3, int initialDelayMs = 1000)
        {
            int delay = initialDelayMs;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    return await action();
                }
                catch (Exception) when (attempt < maxRetries)
                {
                    await Task.Delay(delay);
                    delay *= 2;
                }
            }

            // final attempt - let exceptions bubble
            return await action();
        }

        public void Dispose()
        {
            try
            {
                // Kernel does not implement IDisposable, so just check for IDisposable via Services
                if (_kernel.Services is IDisposable d)
                    d.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception while disposing kernel: {Message}", ex.Message);
            }
        }
    }
}
