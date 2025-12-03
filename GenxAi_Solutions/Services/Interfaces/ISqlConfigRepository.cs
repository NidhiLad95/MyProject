namespace GenxAi_Solutions.Services.Interfaces
{
    public record SqlAnalyticsConfig(string? DatabaseName,string? CompanyName, string? ConnectionString, string? SchemaName,string? TablesSelected, string? ViewsSelected,string? PromptConfiguration,int? CreatedBy);
    public record FileConfigRow(
    int FileConfigID,
    string? FileName,
    string? FilePath,
    string? Description,
    string? PromptConfiguration,
    DateTime? UploadedAt,
    int CompanyID,
    string? CompanyName,
    int? CreatedBy
);

    public interface ISqlConfigRepository
    {
        
        Task<SqlAnalyticsConfig?> GetByCompanyIdAsync(int companyId, CancellationToken ct);
        Task<List<FileConfigRow>> GetFilesByCompanyIdAsync(int companyId, CancellationToken ct);
        

            // NEW: persist the vector DB (SQLite) file name for the company
        Task SaveVectorDbNameAsync(int companyId, string dbFileName, CancellationToken ct);
        Task SavePDFVectorDbNameAsync(int companyId, string dbFileName, CancellationToken ct);
        Task UpdateAnalyticsStatusAsync(int? companyId, string type, CancellationToken ct);
        Task<long> InsertNotificationAsync(
            int companyId,
            int userId,
            string title,
            string message,
            string? linkUrl,
            string process,         // e.g., "Seeding"
            string moduleName,      // e.g., "SQLAnalytics" or "FileAnalytics"
            long? refId,
            string outcome,         // "success" | "fail"
            CancellationToken ct);
    }
}
