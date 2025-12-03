using GenxAi_Solutions_V1.Dtos;
using GenxAi_Solutions_V1.Utils;
using Microsoft.SemanticKernel.Connectors.SqliteVec;

namespace GenxAi_Solutions_V1.Services.Interfaces
{
    public interface IVectorStoreSeedService
    {
        ///// <summary>
        ///// Creates (or opens) the DB via factory and seeds schemas, columns, and prompts.
        ///// Returns the ready-to-use store.
        ///// </summary>
        ////SQLiteVectorStore BuildAndSeed(string dbName, string connectionString);
        //Task SeedAsync(SQLiteVectorStore store, string connectionString, string? schemaName, string? TablesSelected, string ViewsSelected, CancellationToken ct);
        //Task SeedPromptsAsync(SQLiteVectorStore store, string promptstr, CancellationToken ct); // optional

        //// Existing signatures…

        ///// <summary>
        ///// Extracts text from PDFs, splits to sections/chunks, embeds, and stores in DocumentChunks.
        ///// Returns total chunks written.
        ///// </summary>
        //Task<int> SeedDocumentsFromPdfAsync(
        //    string sqliteDbPath,
        //    IEnumerable<string> filePaths,
        //    int sectionMaxWords = 800,
        //    int chunkMaxWords = 200,
        //    CancellationToken ct = default);

        ///// <summary>
        ///// Optional overload when text is already available.
        ///// </summary>
        //Task<int> SeedDocumentsFromTextAsync(
        //    string sqliteDbPath,
        //    string docId,
        //    string text,
        //    int sectionMaxWords = 800,
        //    int chunkMaxWords = 200,
        //    CancellationToken ct = default);

        //Task SeedPdfPromptsAsync(string sqliteDbPath, Dictionary<string, string> pdfprompt, CancellationToken ct);
        //Task<UploadResponse> IngestPdfAsync(string filePath, string sqliteDbPath, string collectionPrefix = "book_");
        //Task<pdfQueryResponse> QueryAsync(string question, string sqliteDbPath, int topSections = 30, int topChunks = 5);

        Task SeedAsyncNew(SqlVectorStores store, string connectionString, string? schemaName, string? TablesSelected, string ViewsSelected, CancellationToken ct);
        Task SeedPromptsAsyncNew(SqlVectorStores store, string promptstr, CancellationToken ct);

        Task SeedPdfPromptsAsyncNew(PdfVectorStores store, string promptstr, CancellationToken ct);
        Task<UploadResponse> IngestPdfAsyncNew(PdfVectorStores store, string filePath, string sqliteDbPath, string collectionPrefix = "book_"); 

    }
}

