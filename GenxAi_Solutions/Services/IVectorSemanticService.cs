namespace GenxAi_Solutions.Services
{
    using GenxAi_Solutions.Dtos;

    public interface IVectorSemanticService : IDisposable
    {
        Task<UploadResponse> IngestPdfAsync(string filePath, string collectionPrefix = "book_");
        Task<QueryResponse> QueryAsync(string question, int topSections = 30, int topChunks = 5);
    }
}
