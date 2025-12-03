using GenxAi_Solutions.Services.Hubs;
using GenxAi_Solutions.Services.Interfaces;
using GenxAi_Solutions.Utils;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;

namespace GenxAi_Solutions.Services
{
    public class SemanticSeeder : ISemanticSeeder
    {
        private readonly IHubContext<SemanticHub> _hub;
        private readonly ISqlConfigRepository _sqlRepo;
        private readonly IVectorStoreFactory _storeFactory;
        private readonly IVectorStoreSeedService _seedService;
        private readonly ILogger<SemanticSeeder> _log;
        private readonly IAuditLogger _audit;

        public SemanticSeeder(
            ISqlConfigRepository sqlRepo,
            IVectorStoreFactory storeFactory,
            IVectorStoreSeedService seedService,
            ILogger<SemanticSeeder> log, IAuditLogger audit)
        {
            _sqlRepo = sqlRepo;
            _storeFactory = storeFactory;
            _seedService = seedService;
            _log = log; _audit = audit;
        }

        public async Task RunSeedAsync(int companyId, CancellationToken ct)
        {

            _log.LogInformation("RunSeedAsync start company={CompanyId}", companyId);
            _audit.LogGeneralAudit("Seed.SQL.Start", "system", "-", $"company={companyId}");

            // 1) pull the company’s analytics SQL connection
            var cfg = await _sqlRepo.GetByCompanyIdAsync(companyId, ct);
            if (cfg is null || string.IsNullOrWhiteSpace(cfg.ConnectionString))
                throw new InvalidOperationException("No SQL analytics configuration found for this Company.");

            // 2) build a per-company SQLite vector DB filename
            var sqliteDbFile = $"db_ai_company_{companyId}.db";
            var sqliteDbPath = Path.Combine(AppContext.BaseDirectory, sqliteDbFile);

            // 2.1) if a file with the same name exists, delete the old file
            if (File.Exists(sqliteDbPath))
            {
                try
                {
                    File.Delete(sqliteDbPath);
                }
                catch (IOException)
                {
                    // If SQLite had a lingering lock, a short retry can help.
                    await Task.Delay(250, ct);
                    File.Delete(sqliteDbPath);
                }
            }

            // 3) create/open store
            var store = _storeFactory.Create(sqliteDbFile);

            try
            {
                // 4) seed schema + columns
                await _seedService.SeedAsync(store, cfg.ConnectionString!, cfg.DatabaseName, cfg.TablesSelected, cfg.ViewsSelected, ct);

                // 5) seed prompts
                await _seedService.SeedPromptsAsync(store, cfg.PromptConfiguration, ct);

                // 6) persist the sqlite DB “name” back to SQL so you can resolve later
                await _sqlRepo.SaveVectorDbNameAsync(companyId, sqliteDbFile, ct);

                // 🔔 ADD: Write SUCCESS notification
                var initiatorUserId = cfg.CreatedBy ?? 0;   // use a real user id if you have it
                var insRes=await _sqlRepo.InsertNotificationAsync(
                    companyId: companyId,
                    userId: initiatorUserId,
                    title: cfg.CompanyName?? "Seeding completed",//"Seeding completed",
                    message: $"Seeding completed for SQL Analytics.",
                    linkUrl: null,
                    process: "Seeding",
                    moduleName: "SQLAnalytics",
                    refId: null,
                    outcome: "success",
                    ct: ct
                );
            }
            catch (Exception ex)
            {
                // 🔔 ADD: Write FAILURE notification
                var initiatorUserId = cfg?.CreatedBy ?? 0;
                var insres= await _sqlRepo.InsertNotificationAsync(
                    companyId: companyId,
                    userId: initiatorUserId,
                    title: cfg?.CompanyName ?? "Seeding failed",
                    message: $"Seeding failed for SQL Analytics.", //ex.Message,          // optional: truncate if too long
                    linkUrl: null,
                    process: "Seeding",
                    moduleName: "SQLAnalytics",
                    refId: null,
                    outcome: "fail",
                    ct: ct
                );

                throw; // keep your existing behavior
            }
            _log.LogInformation("RunSeedAsync done company={CompanyId}", companyId);
            _audit.LogGeneralAudit("Seed.SQL.Done", "system", "-", $"company={companyId}");
        }

        
        public async Task RunSeedPDFAsync(int companyId, CancellationToken ct)
        {
            _log.LogInformation("RunSeedPDFAsync start company={CompanyId}", companyId);
            _audit.LogGeneralAudit("Seed.PDF.Start", "system", "-", $"company={companyId}");

            // 1) fetch all active/saved file rows for this company
            var files = await _sqlRepo.GetFilesByCompanyIdAsync(companyId, ct);
            if (files is null || files.Count == 0)
                return; // nothing to ingest

            // 2) decide per-company vector DB file
            var sqliteDbFile = $"PDFdb_ai_company_{companyId}.db";
            var sqliteDbPath = Path.Combine(AppContext.BaseDirectory, sqliteDbFile);
            var initiatorUserId = files[0].CreatedBy;
            // 2.1) if a file with the same name exists, delete the old file
            if (File.Exists(sqliteDbPath))
            {
                try
                {
                    File.Delete(sqliteDbPath);
                }
                catch (IOException)
                {
                    // If SQLite had a lingering lock, a short retry can help.
                    await Task.Delay(250, ct);
                    File.Delete(sqliteDbPath);
                }
            }

            // 3) collect existing file paths
            var filePaths = files
                .Select(f => f.FilePath)
                //.Where(p => !string.IsNullOrWhiteSpace(p) && System.IO.File.Exists(p!))
                //.Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            //// 4) ingest PDFs -> chunks -> embeddings
            //if (filePaths.Length > 0)
            //{
            //    await _seedService.SeedDocumentsFromPdfAsync(
            //        sqliteDbPath: sqliteDbPath,
            //        filePaths: filePaths,
            //        sectionMaxWords: 800,
            //        chunkMaxWords: 200,
            //        ct: ct);
            //}

            try
            {
                // 4) ingest PDFs -> chunks -> embeddings
                if (filePaths.Length > 0)
                {
                    foreach (var fl in filePaths)
                    {
                        await _seedService.IngestPdfAsync(fl?.ToString() ?? "", sqliteDbPath, "book_");
                    }

                }

                // 5) optional: seed prompts from PromptConfiguration (JSON or CSV) if present
                // Gather all inline prompt configs (JSON or CSV text)
                var promptTexts = files
                    .Select(f => f.PromptConfiguration)
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(p => p!.Trim())
                    .Distinct(StringComparer.Ordinal) // keep exact-case distinctness for content
                    .ToArray();

                var promptDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                //promptDict.Add("Pdf Default", PromptService.BuildPDFPrompt(promptTexts.FirstOrDefault()));

                //Currently not needed
                //promptDict.Add("Pdf Default", PromptService.BuildPDFBookPrompt(promptTexts.FirstOrDefault()));


                promptDict.Add("Pdf Default", PromptService.BuildPDFBookPrompt(promptTexts.FirstOrDefault()));


                if (promptDict.Count > 0)
                {
                    // requires an overload like this in your seed service
                    //await _seedService.SeedPdfPromptsAsync(sqliteDbPath, promptDict, ct);
                    await _seedService.SeedPdfPromptsAsync(sqliteDbPath, promptDict, ct);
                }

                // 6) persist vector DB filename (if you track it in SQL)
                await _sqlRepo.SavePDFVectorDbNameAsync(companyId, sqliteDbFile, ct);


                var insRes = await _sqlRepo.InsertNotificationAsync(
                    companyId: companyId,
                    userId: Convert.ToInt32(initiatorUserId),
                    title: files.FirstOrDefault()?.CompanyName ?? "PDF Seeding completed",
                    message: $"PDF/File seeding completed.",//Vector DB = {sqliteDbFile}. Files: {filePaths.Length}",
                    linkUrl: null,
                    process: "Seeding",
                    moduleName: "FileAnalytics",   // or "PDFAnalytics" if you prefer
                    refId: null,
                    outcome: "success",
                    ct: ct
                );
            }
            catch (Exception ex)
            {
                var insRes = await _sqlRepo.InsertNotificationAsync(
                    companyId: companyId,
                    userId: Convert.ToInt32(initiatorUserId),
                    title: files.FirstOrDefault()?.CompanyName ?? "PDF Seeding completed",
                    message: $"PDF/File seeding Failed.",//Vector DB = {sqliteDbFile}. Files: {filePaths.Length}",
                    linkUrl: null,
                    process: "Seeding",
                    moduleName: "FileAnalytics",   // or "PDFAnalytics" if you prefer
                    refId: null,
                    outcome: "success",
                    ct: ct
                );

                throw; // keep your existing behavior
            }

            _log.LogInformation("RunSeedPDFAsync done company={CompanyId}", companyId);
            _audit.LogGeneralAudit("Seed.PDF.Done", "system", "-", $"company={companyId}");

        }



    }

}
