using BAL.Interface;
using BOL;
using GenxAi_Solutions.Models;
using GenxAi_Solutions.Services.Hubs;
using GenxAi_Solutions.Services.Interfaces;
using GenxAi_Solutions.Utils;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing.Matching;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using Microsoft.Identity.Client;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;
using MySqlX.XDevAPI;
using Newtonsoft.Json;
using Org.BouncyCastle.Utilities.Collections;
using SQLitePCL;
using System.ComponentModel.Design;
using System.Configuration;
using System.Data;
using System.Security.Claims;
using System.Text.RegularExpressions;



namespace GenxAi_Solutions.Api
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class CompanyOnboardController : ControllerBase
    {
        private readonly ICompanyProfileBAL _repo;
        private readonly IWebHostEnvironment _env;
        private readonly IBackgroundJobQueue _queue;
        private readonly IJobStore _jobs;
        private readonly ISemanticSeeder _semanticSeeder;
        private readonly IVectorStoreSeedService _vseedsvc;
        private readonly Kernel _kernel;
        private readonly IChatBotBAL _chat;
        private readonly ILogger<CompanyOnboardController> _logger;
        private readonly ILogger<SQLiteVectorStore> _sqliteLogger;
        private readonly IAuditLogger _auditLogger;
        private readonly IConfiguration _cfg; 



        // How many tables and columns to pick
        private const int TopTables = 5;
        private const int TopColumnsPerTable = 10;
        private const int TopPrompts = 5;

        public CompanyOnboardController(
            ICompanyProfileBAL repo,
            IWebHostEnvironment env,
            IBackgroundJobQueue queue,
            IJobStore jobs,
            ISemanticSeeder semanticSeeder,
            IVectorStoreSeedService vseedsvc,
            Kernel kernel,
            IChatBotBAL chat,
            ILogger<CompanyOnboardController> logger,
            ILogger<SQLiteVectorStore> sqliteLogger,
        IAuditLogger auditLogger,
        IConfiguration cfg
            // SQLiteVectorStore store
            )
        {
            _repo = repo;
            _env = env;
            _queue = queue;
            _jobs = jobs;
            _semanticSeeder = semanticSeeder;
            _vseedsvc = vseedsvc;
            _kernel = kernel;
            _chat = chat;
            _logger = logger;
            _sqliteLogger = sqliteLogger;
            _auditLogger = auditLogger;
            _cfg = cfg;
        }

        [HttpPost("CreateProfile")]
        public async Task<IActionResult> CreateProfile([FromBody] CompanyProfileCreate request)
        {
            var username = User?.Identity?.Name ?? "anonymous";
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var correlationId = HttpContext.TraceIdentifier;

            try
            {
                // INFO Log: Action started
                _logger.LogInformation(
                    "CreateProfile API called - User: {Username}, CompanyName: {CompanyName}, CorrelationId: {CorrelationId}",
                    username, request?.CompanyName, correlationId);

                if (request is null || string.IsNullOrWhiteSpace(request.CompanyName))
                {
                    _logger.LogWarning(
                        "CreateProfile validation failed - User: {Username}, CorrelationId: {CorrelationId}",
                        username, correlationId);
                    return BadRequest(new { message = "CompanyName is required." });
                }

                if (User?.Identity?.IsAuthenticated == true)
            {
                // read int userId from claims
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                        
                    request.CreatedBy = userId;
                        //_logger.LogDebug(
                        //    "User ID {UserId} extracted from claims for CreateProfile - CorrelationId: {CorrelationId}",
                        //    userId, correlationId);
                    }
                else
                {
                    request.CreatedBy = 0;
                }
            }
            else
            {
                request.CreatedBy = 0;
            }

            var spResult = await _repo.CreateCompanyasync(request);

            if (spResult.Status == true && spResult.Data > 0)
            {

                    _logger.LogInformation(
                        "Company profile created successfully -  CompanyId: {CompanyId}, User: {Username}, CorrelationId: {CorrelationId}",
                        spResult.Data, username, correlationId);

                    // AUDIT Log: Company creation
                    _auditLogger.LogDataAccess(
                        username,
                        "CREATE",
                        "CompanyProfile",
                        spResult.Data.ToString(),
                        $"Company profile created - Name: {request.CompanyName}, CorrelationId: {correlationId}");
                    // Created 201 + payload with new ID
                    return CreatedAtAction(nameof(GetById), new { id = spResult.Data },
                    new { message = spResult.Message, companyId = spResult.Data });
            }

                // Duplicate or other business error from SP
                _logger.LogWarning(
                        "Company profile creation failed - Message: {Message}, User: {Username}, CorrelationId: {CorrelationId}",
                        spResult.Message, username, correlationId);

                if (spResult.Message?.Contains("already exists", System.StringComparison.OrdinalIgnoreCase) == true)
                return Conflict(new { message = spResult.Message });

            return BadRequest(new { message = spResult.Message ?? "Insert failed." });
            }
            catch (Exception ex)
            {
                // ERROR Log: Exception
                _logger.LogError(
                    ex,
                    "Error in CreateProfile API - User: {Username}, CompanyName: {CompanyName}, CorrelationId: {CorrelationId}, Error: {ErrorMessage}",
                    username, request?.CompanyName, correlationId, ex.Message);

                // SECURITY Log: Security event for the error
                _auditLogger.LogSecurityEvent(
                    "COMPANY_CREATION_ERROR",
                    username,
                    ipAddress,
                    $"Error creating company profile: {ex.Message}, CorrelationId: {correlationId}");

                throw;
            }
        }

        [HttpPost("SQLAnalytics")]
        public async Task<IActionResult> SaveSqlConfig([FromBody] SQLAnalyticsCreate request)
        {
            var username = User?.Identity?.Name ?? "anonymous";
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var correlationId = HttpContext.TraceIdentifier;

            try
            {
                // INFO Log: Action started
                _logger.LogInformation(
                    "SaveSqlConfig API called - User: {Username}, CorrelationId: {CorrelationId}",
                    username, correlationId);

                if (User?.Identity?.IsAuthenticated == true)
            {
                // read int userId from claims
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                    if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                    {
                        request.CreatedBy = userId;
                        _logger.LogDebug(
                            "User ID {UserId} extracted from claims for SaveSqlConfig - CorrelationId: {CorrelationId}",
                            userId, correlationId);
                    }
                    else
                    {
                        request.CreatedBy = 0;
                    }
                }
            else
            {
                request.CreatedBy = 0;
            }


            var spResult = await _repo.SaveSqlAnalyticConfigasync(request);

            if (spResult.Status == true && spResult.Data > 0)
            {
                    // INFO Log: Success
                    _logger.LogInformation(
                        "SQL Analytics config saved successfully - ConfigId: {ConfigId}, User: {Username}, CorrelationId: {CorrelationId}",
                        spResult.Data, username, correlationId);

                    // AUDIT Log: SQL config creation
                    _auditLogger.LogDataAccess(
                        username,
                        "CREATE",
                        "SQLAnalytics",
                        spResult.Data.ToString(),
                        $"SQL Analytics configuration created, CorrelationId: {correlationId}");

                    // Created 201 + payload with new ID
                    _logger.LogWarning(
                   "SQL Analytics config save failed - Message: {Message}, User: {Username}, CorrelationId: {CorrelationId}",
                   spResult.Message, username, correlationId);


                    return CreatedAtAction(nameof(GetById), new { id = spResult.Data },
                    new { message = spResult.Message, sqlCofigId = spResult.Data });
            }

            // Duplicate or other business error from SP
            if (spResult.Message?.Contains("already exists", System.StringComparison.OrdinalIgnoreCase) == true)
                return Conflict(new { message = spResult.Message });

            return BadRequest(new { message = spResult.Message ?? "Insert failed." });
            }
            catch (Exception ex)
            {
                // ERROR Log: Exception
                _logger.LogError(
                    ex,
                    "Error in SaveSqlConfig API - User: {Username}, CorrelationId: {CorrelationId}, Error: {ErrorMessage}",
                    username, correlationId, ex.Message);

                throw;
            }
        }



        [HttpPost("FileAnalytics")]
        public async Task<IActionResult> SaveFileConfig(
    [FromForm] List<IFormFile> files,
    [FromForm] List<string> descriptions,
    [FromForm] FileAnalyticsCreate_req request)   // <-- switched to [FromForm]
        {
            var username = User?.Identity?.Name ?? "anonymous";
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var correlationId = HttpContext.TraceIdentifier;

            try
            {
                // INFO Log: Action started
                _logger.LogInformation(
                    "SaveFileConfig API called - User: {Username}, FileCount: {FileCount}, CompanyID: {CompanyID}, CorrelationId: {CorrelationId}",
                    username, files?.Count ?? 0, request.CompanyID, correlationId);

                // Fill CreatedBy from claims if available
                if (User?.Identity?.IsAuthenticated == true)
                {
                    var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                    request.CreatedBy = (userIdClaim != null && int.TryParse(userIdClaim.Value, out var uid)) ? uid : 0;
                    _logger.LogDebug(
                        "User ID {UserId} extracted from claims for SaveFileConfig - CorrelationId: {CorrelationId}",
                        request.CreatedBy, correlationId);
                }
                else
                {
                    request.CreatedBy = 0;
                }

                var objSave = new List<FileAnalyticsCreate>();

            try
            {
                    if (files == null || files.Count == 0)
                    {
                        _logger.LogWarning(
                            "No files uploaded in SaveFileConfig - User: {Username}, CorrelationId: {CorrelationId}",
                            username, correlationId);
                        return BadRequest("No files uploaded.");
                    }

                    var webRoot = _env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
                var uploadDir = Path.Combine(webRoot, "Uploads/PDF/" + request.CompanyName);
                Directory.CreateDirectory(uploadDir);

                    _logger.LogInformation(
                        "Upload directory created/verified: {UploadDir}, CorrelationId: {CorrelationId}",
                        uploadDir, correlationId);

                    for (int i = 0; i < files.Count; i++)
                {
                    var file = files[i];
                    if (file?.Length > 0)
                    {
                        var safeName = Path.GetFileName(file.FileName);
                        var uniqueName = $"{DateTime.UtcNow.Ticks}_{safeName}";
                        var fullPath = Path.Combine(uploadDir, safeName);

                        await using (var stream = System.IO.File.Create(fullPath))
                            await file.CopyToAsync(stream);

                            _logger.LogDebug(
                                    "File uploaded successfully: {FileName}, Size: {FileSize}, CorrelationId: {CorrelationId}",
                                    safeName, file.Length, correlationId);

                            objSave.Add(new FileAnalyticsCreate
                        {
                            FileName = safeName,
                            FilePath = fullPath.Replace('\\', '/'),
                            Description = i < descriptions.Count ? descriptions[i] : null,
                            PromptConfiguration = request.PromptConfiguration, // from form
                            UploadedAt = DateTime.UtcNow,
                            CompanyID = request.CompanyID,
                            flgSave = 1,
                            CreatedBy = request.CreatedBy
                        });
                    }
                }
            }catch (Exception fileEx)
                {
                    _logger.LogError(
                        fileEx,
                        "File upload failed in SaveFileConfig - User: {Username}, CorrelationId: {CorrelationId}",
                        username, correlationId);
                    throw;
                }

                var spResult = await _repo.SaveFileAnalyticsConfigAsync(objSave);

            if (spResult.Status == true && spResult.Data > 0)
            {
                    // INFO Log: Success
                    _logger.LogInformation(
                        "File Analytics config saved successfully - ConfigId: {ConfigId}, User: {Username}, FilesProcessed: {FileCount}, CorrelationId: {CorrelationId}",
                        spResult.Data, username, objSave.Count, correlationId);

                    // AUDIT Log: File config creation
                    _auditLogger.LogDataAccess(
                        username,
                        "CREATE",
                        "FileAnalytics",
                        spResult.Data.ToString(),
                        $"File Analytics configuration created with {objSave.Count} files, CorrelationId: {correlationId}");

                    return CreatedAtAction(nameof(GetById),
                    new { id = spResult.Data },
                    new { message = spResult.Message, fileCofigId = spResult.Data });
            }

                _logger.LogWarning(
                        "File Analytics config save failed - Message: {Message}, User: {Username}, CorrelationId: {CorrelationId}",
                        spResult.Message, username, correlationId);

                if (spResult.Message?.Contains("already exists", StringComparison.OrdinalIgnoreCase) == true)
                return Conflict(new { message = spResult.Message });

            return BadRequest(new { message = spResult.Message ?? "Insert failed." });
            }
            catch (Exception ex)
            {
                // ERROR Log: Exception
                _logger.LogError(
                    ex,
                    "Error in SaveFileConfig API - User: {Username}, CorrelationId: {CorrelationId}, Error: {ErrorMessage}",
                    username, correlationId, ex.Message);

                throw;
            }
        }


        [HttpPost("SavePermanent")]
        public async Task<IActionResult> SaveProfilePermanent([FromBody] SaveProfilePermanent request, CancellationToken cancellationToken)
        {
            var username = User?.Identity?.Name ?? "anonymous";
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var correlationId = HttpContext.TraceIdentifier;

            try
            {
                // INFO Log: Action started
                _logger.LogInformation(
                    "SaveProfilePermanent API called - User: {Username}, CorrelationId: {CorrelationId}",
                    username, correlationId);

                // Set UpdatedBy from claims (unchanged)
                if (User?.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                request.UpdatedBy = (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId)) ? userId : 0;

                    _logger.LogDebug(
                        "User ID {UserId} extracted from claims for SaveProfilePermanent - CorrelationId: {CorrelationId}",
                        request.UpdatedBy, correlationId);
                }
            else
            {
                request.UpdatedBy = 0;
            }

            var spResult = await _repo.SaveprofileConfigPermanentAsync(request);

            if (spResult.Status == true && spResult.Data > 0)
            {
                var companyId = spResult.Data;

                    _logger.LogInformation(
                        "Profile saved permanently - CompanyId: {CompanyId}, User: {Username}, CorrelationId: {CorrelationId}",
                        companyId, username, correlationId);

                    // AUDIT Log: Permanent save
                    _auditLogger.LogGeneralAudit(
                        "PROFILE_SAVE_PERMANENT",
                        username,
                        ipAddress,
                        $"Company profile saved permanently - CompanyId: {companyId}, CorrelationId: {correlationId}");

                    // ✅ Do NOT run seeding here. Just return success + ID.              
                    return CreatedAtAction(nameof(GetById), new { id = companyId },
                    new { message = spResult.Message, CompanyId = companyId });
            }

                // Duplicate or other business error from SP
                _logger.LogWarning(
                        "Permanent profile save failed - Message: {Message}, User: {Username}, CorrelationId: {CorrelationId}",
                        spResult.Message, username, correlationId);

                if (spResult.Message?.Contains("already exists", StringComparison.OrdinalIgnoreCase) == true)
                return Conflict(new { message = spResult.Message });

            return BadRequest(new { message = spResult.Message ?? "Updation failed." });
            }
            catch (Exception ex)
            {
                // ERROR Log: Exception
                _logger.LogError(
                    ex,
                    "Error in SaveProfilePermanent API - User: {Username}, CorrelationId: {CorrelationId}, Error: {ErrorMessage}",
                    username, correlationId, ex.Message);

                throw;
            }
        }


        [HttpGet("databasetype")]
        public async Task<ActionResult> GetDatabaseType()
        {

            var list = await _repo.GetDatabasesDdl();
            return Ok(list);
        }

        [HttpPost("getdatabase")]
        public async Task<ActionResult> GetDatabases(GetSchemaDdl model)
        {
            // model.ConnectionString should be a VALID SQL Server connection string
            if (model is null || string.IsNullOrWhiteSpace(model.ConnectionString))
                return BadRequest(new { message = "ConnectionString is required." });

            List<DatabaseDDL> lstdb = new List<DatabaseDDL>();
            try
            {
                if (model.DatabaseType == "SQL Server")
                {
                    lstdb = await DatabaseReading.GetDatabasesAsync(model.ConnectionString);
                }
                else if (model.DatabaseType == "MySQL")
                {
                    lstdb = await DatabaseReading.GetDatabasesAsync_MySql(model.ConnectionString);
                }
                else if (model.DatabaseType == "PostgreSQL")
                {
                    lstdb = await DatabaseReading.GetDatabasesAsync_Pg(model.ConnectionString);
                }
            }
            catch (SqlException ex)
            {
                // Surface helpful info without leaking secrets
                return StatusCode(500, new { message = "SQL error while listing databases.", detail = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Unexpected error while listing databases.", detail = ex.Message });
            }

            //var list = await DatabaseReading.GetDatabasesAsync(model.ConnectionString);
            return Ok(lstdb);
        }

        [HttpPost("databaseinfo")]
        public async Task<ActionResult> DatabaseInfo([FromBody] ConnectionDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.ConnectionString) || string.IsNullOrWhiteSpace(dto.Database))
                return BadRequest("ConnectionString and Database are required.");

            //var sb = new SqlConnectionStringBuilder(dto.ConnectionString)
            //{
            //    InitialCatalog = dto.Database, // we’ll still ChangeDatabase in the helper
            //    TrustServerCertificate = true
            //};
            List<DatabaseInfo> lstdb = new List<DatabaseInfo>();

            if (dto.DatabaseType == "SQL Server")
            {
                lstdb = await DatabaseReading.GetDatabseInfoAsync(dto.ConnectionString, dto.Database);
            }
            else if (dto.DatabaseType == "MySQL")
            {
                lstdb = await DatabaseReading.GetDatabseInfoAsync_MySql(dto.ConnectionString, dto.Database);
            }
            else if (dto.DatabaseType == "PostgreSQL")
            {
                lstdb = await DatabaseReading.GetDatabseInfoAsync_Pg(dto.ConnectionString, dto.Database);
            }

            // var list = await DatabaseReading.GetDatabseInfoAsync(dto.ConnectionString, dto.Database);
            var schemalst = lstdb.DistinctBy(x => x.SchemaID).Select(x => new DatabaseDDL { Text = x.SchemaName, Value = x.SchemaID }).ToList();
            var tablelst = lstdb.Where(x => x.ObjectType == 'U').Select(x => new TableViewInfo { ObjectName = x.ObjectName, ObjectID = x.ObjectID, SchemaName = x.SchemaName }).ToList();
            var viewlst = lstdb.Where(x => x.ObjectType == 'V').Select(x => new TableViewInfo { ObjectName = x.ObjectName, ObjectID = x.ObjectID, SchemaName = x.SchemaName }).ToList();

            return Ok(new { SchemaList = schemalst, TableList = tablelst, ViewList = viewlst });
        }

        #region commented

        static string GetText(ChatMessageContent msg)
        {
            if (msg == null) return "";
            if (!string.IsNullOrEmpty(msg.Content)) return msg.Content;
            // Fallback: concatenate any TextContent parts
            var parts = msg.Items?.OfType<TextContent>()?.Select(t => t.Text) ?? Enumerable.Empty<string>();
            return string.Concat(parts);
        }

        private const string ContextMarker = "[[ctx:v1]]";
        private sealed record UserCtx(
            int UserId,
            int CompanyId,
            string? DatabaseName,
            string? ConnStr,
            string? DbType,
            string? SqliteDbPath_Sql,
            string? SqliteDbPath_File
        );
        private sealed record AskReturn(
            string Message,
            long ConversationId,
            int TokensUsed,
            List<Dictionary<string, object>> Data,
            object? Chart = null,
            string? NewTitle = null
        );

       

        [HttpPost("Ask1")]
        [Consumes("application/x-www-form-urlencoded")]
        public async Task<IActionResult> AskChat(
    [FromForm] string message,
    [FromForm] string? service,
    [FromForm] long? conversationId,
    CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(message))
                return BadRequest(new { error = "Empty message" });

            // --- session/state (claims-first like the rest of your controller)
            string databaseName = "", dbPathSql = "", connStr = "", dbType = "", dbPathPdf_File = "";
            int companyId = 0, userId = 0;
            long convId = conversationId ?? 0;
            var chosenService = string.IsNullOrWhiteSpace(service) ? "SQLAnalytics" : service;

            if (User?.Identity?.IsAuthenticated == true)
            {
                var uidClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                int.TryParse(uidClaim?.Value, out userId);

                databaseName = User.FindFirstValue("DatabaseName");
                dbPathSql = User.FindFirstValue("SQLitedbName");
                connStr = User.FindFirstValue("Connstr");
                dbType = User.FindFirstValue("dbType");          // e.g., "SQL Server"
                companyId = Convert.ToInt32(User.FindFirstValue("CompanyId"));
                dbPathPdf_File = User.FindFirstValue("SQLitedbName_File");
            }

            //var chosenService = string.IsNullOrWhiteSpace(service) ? "SQLAnalytics" : service;

            // detect chart intent from user text
            var wantsChart = System.Text.RegularExpressions.Regex.IsMatch(
                message, @"\b(chart|graph(ical)|plot|visual(ize|isation|ization)?)\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            long userMsgId = 0;
            long asstMsgId = 0;

            //// BAL already injected into controller as _chat
            //long convId = conversationId ?? 0;
            //int userId = 0;
            if (User?.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var uid)) userId = uid;
            }

            // Create conversation lazily
            if (_chat != null && convId == 0)
            {
                string titleSeed = message.Length > 40 ? message.Substring(0, 40) + "…" : message;
                convId = (await _chat.StartConversationAsync(new StartConversation
                {
                    UserId = userId,
                    Title = string.IsNullOrWhiteSpace(titleSeed) ? "New chat" : titleSeed
                })).Data;

                // remember chosen service as a system message
                try
                {
                    await _chat.AppendMessageAsync(new AppendMessage
                    {
                        ConversationId = convId,
                        SenderType = "system",
                        SenderId = null,
                        Text = $"service={chosenService}"
                    });
                }
                catch { /* ignore */ }
            }

            // Persist user message
            if (_chat != null && convId > 0)
            {
                var appendResp = await _chat.AppendMessageAsync(new AppendMessage
                {
                    ConversationId = convId,
                    SenderType = "user",
                    SenderId = userId,
                    Text = message
                });
                userMsgId = appendResp.Data;

                await _chat.AddMetadataBulkAsync(userMsgId, new Dictionary<string, string>
                {
                    ["service"] = chosenService,
                    ["wantsChart"] = wantsChart ? "1" : "0"
                });
            }

            // embedder + token estimator
            var embedder = _kernel.GetRequiredService<ITextEmbeddingGenerationService>("openai-embed");
            var queryEmb = (await embedder.GenerateEmbeddingAsync(message)).ToArray();
            static int EstimateTokens(string? text) => string.IsNullOrEmpty(text) ? 0 : Math.Max(1, text.Length / 4);
            int tokensUsed = 0;

            // Helper: will hold whatever final assistant “summary text” we used to make a title
            string? replyTextForTitle = null;
            string? newTitle = null;

            // ==================== FileAnalytics ====================
            if (string.Equals(chosenService, "FileAnalytics", StringComparison.OrdinalIgnoreCase))
            {
                //serviceType = 1;
                if (string.IsNullOrWhiteSpace(dbPathPdf_File))
                    return Ok(new
                    {
                        message = "File index not found. Please seed PDFs first.",
                        data = new List<object>(),
                        tokensUsed,
                        conversationId = convId
                    });

                var store = new SQLiteVectorStore(dbPathPdf_File, _sqliteLogger);

                // Get context from your service
                var topchunks = (_vseedsvc.QueryAsync(message, dbPathPdf_File, 30, 5)).Result;

                if (_chat != null && userMsgId > 0)
                {
                    await _chat.AddMetadataBulkAsync(userMsgId, new Dictionary<string, string>
                    {
                        ["Context"] = topchunks.Context?.ToString() ?? "",
                        ["docIds"] = string.Join(", ", topchunks.TopBooks)
                    });
                }

                if (topchunks.TopBooks.Count() == 0)
                {
                    var msg = "No relevant document context found. Please upload/seed PDFs.";
                    if (_chat != null && convId > 0)
                        await _chat.AppendMessageAsync(new AppendMessage { ConversationId = convId, SenderType = "assistant", SenderId = null, Text = msg });

                    // still return usage + conv id
                    return Ok(new { message = msg, data = new List<object>(), tokensUsed, conversationId = convId });
                }

                // nearest prompts
                //var nearestPrompts = store.GetNearestPrompts(queryEmb, topK: 5).ToList();
                //var nearestPrompts = store.GetNearestPrompts(queryEmb, topK: 5).ToList();
                //var chosenPromptText = nearestPrompts.FirstOrDefault().Entry?.Text;
                //if (_chat != null && userMsgId > 0 && nearestPrompts.Count > 0)
                //    await _chat.AddMetadataAsync(userMsgId, "filePromptHitCount", nearestPrompts.Count.ToString());

                //                if (string.IsNullOrWhiteSpace(chosenPromptText))
                //                {
                //                    chosenPromptText =
                //        @"You are an assistant that answers strictly from the provided context.
                //If the answer is not contained in the context, reply: 'I do not have that information in the indexed documents.'
                //Return a concise, direct answer.";
                //                }

                string chosenPromptText =
                @"User said:
                {{$input}}

                Relevant context:
                {{$context}}";


                string context = topchunks.Context;
                var finalPrompt = PromptService.PDFAddUserInputBookInPrompt(
                    chosenPromptText,
                    message,
                    context,
                    topchunks.TopBooks
                );





                tokensUsed += EstimateTokens(context) + EstimateTokens(finalPrompt);

                var history = GetLimitedHistory(conversationId: convId, maxHistory: 20, userMessage: finalPrompt, chosenService: chosenService);
                var chat = _kernel.Services.GetRequiredService<IChatCompletionService>();
                var reply = await chat.GetChatMessageContentsAsync(history, cancellationToken: ct);
                var ans = reply.FirstOrDefault()?.Content?.ToString() ?? "(no answer)";
                GetLimitedHistory(conversationId: convId, maxHistory: 20, assistantMessage: ans, chosenService: chosenService);
                tokensUsed += EstimateTokens(ans);

                // persist assistant
                if (_chat != null && convId > 0)
                {
                    var asstResp = await _chat.AppendMessageAsync(new AppendMessage
                    {
                        ConversationId = convId,
                        SenderType = "assistant",
                        SenderId = null,
                        Text = ans
                    });
                    asstMsgId = asstResp.Data;

                    await _chat.AddMetadataBulkAsync(asstMsgId, new Dictionary<string, string>
                    {
                        ["mode"] = "text",
                        ["tokensUsed"] = tokensUsed.ToString()
                    });

                    await _chat.IncrementConversationTokensAsync(new IncrementConversationTokens { ConversationId = convId, Tokens = tokensUsed });
                    await _chat.IncrementCompanyTokensAsync(new IncrementCompanyTokens { companyId = companyId, Tokens = tokensUsed });
                }

                // ======= NEW: Title update
                replyTextForTitle = ans;
                if (_chat != null && convId > 0)
                {
                    newTitle = await TryUpdateTitleAsync(convId, message, replyTextForTitle, ct);
                }

                return Ok(new
                {
                    message = ans,
                    data = new List<object>(),
                    picke = new { chunks = topchunks.TopBooks.Count() },
                    tokensUsed,
                    conversationId = convId,
                    newTitle // <--
                });
            }

            // ==================== SQLAnalytics ====================
            if (string.IsNullOrWhiteSpace(dbPathSql))
                return Ok(new
                {
                    message = "Schema store not found. Please seed database schema first.",
                    data = new List<object>(),
                    tokensUsed,
                    conversationId = convId
                });

            var store_sql = new SQLiteVectorStore(dbPathSql, _sqliteLogger);
            Console.WriteLine($"SchemaEntries count = {store_sql.CountSchemaRows()}");

            var topSchemas = store_sql.GetNearest(queryEmb, TopTables).ToList();
            if (topSchemas.Count == 0)
                return Ok(new
                {
                    message = "No schema available in store. Seed schema first.",
                    data = new List<object>(),
                    tokensUsed,
                    conversationId = convId
                });

            var selectedColumnsByTable = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var selectedSchemaEntries = new List<SchemaEntry>();

            foreach (var s in topSchemas)
            {
                var columns = store_sql.GetColumnsForTable(s.TableName);
                var semanticTop = store_sql.GetNearestColumns(queryEmb, s.TableName, topK: 1000)
                                           .Select(x => (Name: x.Entry.ColumnName, Score: x.Score))
                                           .ToList();

                var scores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                foreach (var (Name, Score) in semanticTop)
                    if (!string.IsNullOrWhiteSpace(Name))
                        scores[Name] = Math.Max(scores.TryGetValue(Name, out var v) ? v : 0, 0.70 * Score);

                var normQuery = TextSimilarity.Normalize(message);
                foreach (var c in columns)
                {
                    var colName = c.ColumnName;
                    if (string.IsNullOrWhiteSpace(colName)) continue;

                    var normCol = TextSimilarity.Normalize(colName);
                    var normBoth = TextSimilarity.Normalize($"{s.TableName} {colName}");
                    var fz1 = TextSimilarity.JaroWinkler(normQuery, normCol);
                    var fz2 = TextSimilarity.JaroWinkler(normQuery, normBoth);
                    var fz = Math.Max(fz1, fz2);

                    scores[colName] = (scores.TryGetValue(colName, out var v) ? v : 0) + 0.30 * fz;
                }

                var pickedCols = scores.OrderByDescending(kv => kv.Value)
                                       .Take(TopColumnsPerTable)
                                       .Select(kv => kv.Key)
                                       .ToList();

                selectedColumnsByTable[s.TableName] = pickedCols;

                var schemaText = new System.Text.StringBuilder();
                schemaText.AppendLine($"Table: {s.TableName}");
                schemaText.AppendLine("Columns:");
                foreach (var c in pickedCols) schemaText.AppendLine($" - {c}");
                var emb = (await embedder.GenerateEmbeddingAsync(schemaText.ToString())).ToArray();
                selectedSchemaEntries.Add(new SchemaEntry(s.TableName, schemaText.ToString(), emb));
            }

            var schemaHintsJson = Newtonsoft.Json.JsonConvert.SerializeObject(selectedColumnsByTable);
            var schemaHintsEmb = (await embedder.GenerateEmbeddingAsync(schemaHintsJson)).ToArray();
            selectedSchemaEntries.Add(new SchemaEntry("__selected_schema__", schemaHintsJson, schemaHintsEmb));

            //var nearestPromptsSql = store_sql.GetNearestPrompts(queryEmb, TopPrompts).ToList();
            //var chosenPrompt = nearestPromptsSql.FirstOrDefault().Entry?.Text ?? PromptService.Rules;

            var chosenPrompt = PromptService.sqlUserPrompt;

            var finalPromptSql = PromptService.AddUserInputInPrompt(chosenPrompt, message, selectedSchemaEntries);
            tokensUsed += EstimateTokens(finalPromptSql);

            var chatSql = _kernel.Services.GetRequiredService<IChatCompletionService>();
            var historySql = GetLimitedHistory(conversationId: convId, maxHistory: 20, userMessage: finalPromptSql, chosenService: chosenService);
            var replySql = await chatSql.GetChatMessageContentsAsync(historySql, cancellationToken: ct);
            Console.WriteLine("Raw reply: " + replySql.FirstOrDefault()?.Content?.ToString());
            var answer = replySql.FirstOrDefault()?.Content?.ToString() ?? "(no answer)";
            GetLimitedHistory(conversationId: convId, maxHistory: 20, assistantMessage: answer, chosenService: chosenService);

            var raw = replySql.FirstOrDefault()?.Content?.ToString() ?? string.Empty;
            var sql = raw.Replace("```sql", "").Replace("```", "").Trim();

            if (_chat != null && userMsgId > 0 && !string.IsNullOrWhiteSpace(sql))
                await _chat.AddMetadataAsync(userMsgId, "sql", sql);

            tokensUsed += EstimateTokens(sql);

            if (string.IsNullOrWhiteSpace(sql))
            {
                var msg = "Model did not return SQL.";
                if (_chat != null && convId > 0)
                    await _chat.AppendMessageAsync(new AppendMessage { ConversationId = convId, SenderType = "assistant", SenderId = null, Text = msg });

                // try title update too
                replyTextForTitle = msg;
                if (_chat != null && convId > 0) newTitle = await TryUpdateTitleAsync(convId, message, replyTextForTitle, ct);

                return Ok(new
                {
                    message = msg,
                    data = new List<object>(),
                    columnsPicked = selectedColumnsByTable,
                    tokensUsed,
                    conversationId = convId,
                    newTitle
                });
            }

            // Execute SQL
            var rows = new List<Dictionary<string, object>>();
            var table = new DataTable();
            try
            {
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync(ct);
                conn.ChangeDatabase(databaseName);
                using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 120 };
                //using var cmd = new SqlCommand(candidateSql, conn) { CommandTimeout = 120 };
                using var reader = await cmd.ExecuteReaderAsync(ct);
                table.Load(reader);

                foreach (DataRow dr in table.Rows)
                {
                    var r = new Dictionary<string, object>();
                    foreach (DataColumn c in table.Columns)
                        r[c.ColumnName] = dr[c];
                    rows.Add(r);
                }
            }
            catch (Exception ex)
            {
                var err = "Error executing query: " + ex.Message;
                if (_chat != null && convId > 0)
                    await _chat.AppendMessageAsync(new AppendMessage { ConversationId = convId, SenderType = "assistant", SenderId = null, Text = err });

                replyTextForTitle = err;
                if (_chat != null && convId > 0) newTitle = await TryUpdateTitleAsync(convId, message, replyTextForTitle, ct);

                return Ok(new
                {
                    message = err,
                    data = new List<object>(),
                    columnsPicked = selectedColumnsByTable,
                    tokensUsed,
                    conversationId = convId,
                    newTitle
                });
            }


            // Chart branch
            if (wantsChart && table != null && table.Columns.Count > 0 && table.Rows.Count > 0)
            {
                var (specJson, chartSpecTokens) = await GenerateChartSpecAsync(table, message, ct);
                tokensUsed += chartSpecTokens;

                if (_chat != null && convId > 0)
                {
                    var asstResp2 = await _chat.AppendMessageAsync(new AppendMessage
                    { ConversationId = convId, SenderType = "assistant", SenderId = null, Text = "[chart result]" });
                    asstMsgId = asstResp2.Data;

                    var colJson = System.Text.Json.JsonSerializer.Serialize(table.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
                    await _chat.AddMetadataBulkAsync(asstMsgId, new Dictionary<string, string>
                    {
                        ["mode"] = "chart",
                        ["rowCount"] = table.Rows.Count.ToString(),
                        ["columns"] = colJson,
                        ["chartSpec"] = specJson,
                        ["tokensUsed"] = tokensUsed.ToString()
                    });

                    await _chat.IncrementConversationTokensAsync(new IncrementConversationTokens { ConversationId = convId, Tokens = tokensUsed });
                    await _chat.IncrementCompanyTokensAsync(new IncrementCompanyTokens { companyId = companyId, Tokens = tokensUsed });
                }

                // ======= NEW: Title update
                replyTextForTitle = $"Chart for {table.Rows.Count} rows";
                if (_chat != null && convId > 0) newTitle = await TryUpdateTitleAsync(convId, message, replyTextForTitle, ct);

                return Ok(new
                {
                    message = "Here’s a chart for your result.",
                    data = rows,
                    chart = new { spec = specJson, data = DataTableToPlain(table) },
                    tokensUsed,
                    conversationId = convId,
                    newTitle // <--
                });
            }

            // Table / plain result
            var okMsg = rows.Count == 0 ? "No data found." : "Here are your results:";
            if (_chat != null && convId > 0)
            {
                var asstResp3 = await _chat.AppendMessageAsync(new AppendMessage
                { ConversationId = convId, SenderType = "assistant", SenderId = null, Text = rows.Count == 0 ? okMsg : "[table result]" });
                asstMsgId = asstResp3.Data;

                var colJson = System.Text.Json.JsonSerializer.Serialize(table.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
                await _chat.AddMetadataBulkAsync(asstMsgId, new Dictionary<string, string>
                {
                    ["mode"] = "table",
                    ["rowCount"] = table.Rows.Count.ToString(),
                    ["columns"] = colJson,
                    ["tokensUsed"] = tokensUsed.ToString()
                });

                await _chat.IncrementConversationTokensAsync(new IncrementConversationTokens { ConversationId = convId, Tokens = tokensUsed });
                await _chat.IncrementCompanyTokensAsync(new IncrementCompanyTokens { companyId = companyId, Tokens = tokensUsed });
            }

            // ======= NEW: Title update
            replyTextForTitle = rows.Count == 0 ? okMsg :
                $"Table result: {rows.Count} rows from {string.Join(", ", selectedColumnsByTable.Keys.Take(2))}{(selectedColumnsByTable.Count > 2 ? "…" : "")}";
            if (_chat != null && convId > 0) newTitle = await TryUpdateTitleAsync(convId, message, replyTextForTitle, ct);

            return Ok(new
            {
                message = okMsg,
                data = rows,
                columnsPicked = selectedColumnsByTable,
                tokensUsed,
                conversationId = convId,
                newTitle // <--
            });
        }

        [HttpPost("Ask2")]
        [Consumes("application/x-www-form-urlencoded")]
        public async Task<IActionResult> AskChat_up(
    [FromForm] string message,
    [FromForm] string? service,
    [FromForm] long? conversationId,
    CancellationToken ct)
        {
            // just before you call the LLM (around line ~1333)
            var sw = System.Diagnostics.Stopwatch.StartNew();
            //using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
        ct,
        HttpContext?.RequestAborted ?? CancellationToken.None
    );
            // give the model enough time; move to config if you prefer
            
            var timeoutSec = _cfg.GetValue<int?>("OpenAI:NetworkTimeoutSeconds") ?? 120;
            linkedCts.CancelAfter(TimeSpan.FromSeconds(timeoutSec));

            var username = User?.Identity?.Name ?? "anonymous";
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var correlationId = HttpContext.TraceIdentifier;

            try
            {
                

                if(string.IsNullOrWhiteSpace(message))
                {
                    

                    return BadRequest(new { error = "Empty message" });
                }

                // --- claims-first state
                string databaseName = "", dbPathSql = "", connStr = "", dbType = "", dbPathPdf_File = "";
            int companyId = 0, userId = 0;
            long convId = conversationId ?? 0;
            var chosenService = string.IsNullOrWhiteSpace(service) ? "SQLAnalytics" : service;

            if (User?.Identity?.IsAuthenticated == true)
            {
                var uidClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                int.TryParse(uidClaim?.Value, out userId);

                databaseName = User.FindFirstValue("DatabaseName");
                dbPathSql = User.FindFirstValue("SQLitedbName");
                connStr = User.FindFirstValue("Connstr");
                dbType = User.FindFirstValue("dbType");
                companyId = Convert.ToInt32(User.FindFirstValue("CompanyId"));
                dbPathPdf_File = User.FindFirstValue("SQLitedbName_File");

                   
                }

                

            // detect chart intent
            var wantsChart = System.Text.RegularExpressions.Regex.IsMatch(
                message, @"\b(chart|graph(ical)|plot|visual(ize|isation|ization)?)\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                var sqlGreets = IsGreeting(message);

                
                long userMsgId = 0;
            long asstMsgId = 0;

            

            // Create conversation lazily
            if (_chat != null && convId == 0)
            {
                string titleSeed = message.Length > 40 ? message.Substring(0, 40) + "…" : message;
                convId = (await _chat.StartConversationAsync(new StartConversation
                {
                    UserId = userId,
                    Title = string.IsNullOrWhiteSpace(titleSeed) ? "New chat" : titleSeed
                })).Data;

                    //// Seed exactly one governing system prompt in DB (no extra "service=..." system line).
                    //var rules = string.Equals(chosenService, "FileAnalytics", StringComparison.OrdinalIgnoreCase)
                    //    ? "You are a helpful assistant. Use prior messages as context."
                    //    : PromptService.Rules;
                    var sysPrompt = GetPrompt(Convert.ToInt32(User.FindFirstValue("CompanyId")), chosenService);
                    // Exactly one governing system prompt for the LLM context.
                    var rules = string.Equals(chosenService, "FileAnalytics", StringComparison.OrdinalIgnoreCase)
                        ? sysPrompt + "/n/n" + " You are a helpful assistant. Use prior messages as context."+"/n/n"+ PromptService.Pdfrule
                        : sysPrompt + "/n/n" + PromptService.Rules;

                    await _chat.AppendMessageAsync(new AppendMessage
                {
                    ConversationId = convId,
                    SenderType = "system",
                    SenderId = null,
                    Text = rules
                });
            }

            // Persist user message (+ metadata)
            if (_chat != null && convId > 0)
            {
                var appendResp = await _chat.AppendMessageAsync(new AppendMessage
                {
                    ConversationId = convId,
                    SenderType = "user",
                    SenderId = userId,
                    Text = message
                });
                userMsgId = appendResp.Data;

                await _chat.AddMetadataBulkAsync(userMsgId, new Dictionary<string, string>
                {
                    ["service"] = chosenService,
                    ["wantsChart"] = wantsChart ? "1" : "0"
                });
            }

            // embedder + token estimator
            var embedder = _kernel.GetRequiredService<ITextEmbeddingGenerationService>("openai-embed");
            var queryEmb = (await embedder.GenerateEmbeddingAsync(message)).ToArray();
            static int EstimateTokens(string? text) => string.IsNullOrEmpty(text) ? 0 : Math.Max(1, text.Length / 4);
            int tokensUsed = 0;

            string? replyTextForTitle = null;
            string? newTitle = null;

            // ==================== FileAnalytics ====================
            if (string.Equals(chosenService, "FileAnalytics", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(dbPathPdf_File))
                    return Ok(new
                    {
                        message = "File index not found. Please seed PDFs first.",
                        data = new List<object>(),
                        tokensUsed,
                        conversationId = convId
                    });

                var store = new SQLiteVectorStore(dbPathPdf_File, _sqliteLogger);

                // Await instead of .Result
                var topchunks = await _vseedsvc.QueryAsync(message, dbPathPdf_File, 30, 5);

                if (_chat != null && userMsgId > 0)
                {
                    await _chat.AddMetadataBulkAsync(userMsgId, new Dictionary<string, string>
                    {
                        ["Context"] = topchunks.Context?.ToString() ?? "",
                        ["docIds"] = string.Join(", ", topchunks.TopBooks)
                    });
                }

                if (topchunks.TopBooks.Count() == 0)
                {
                    var msg = "No relevant document context found. Please upload/seed PDFs.";
                    if (_chat != null && convId > 0)
                        await _chat.AppendMessageAsync(new AppendMessage { ConversationId = convId, SenderType = "assistant", SenderId = null, Text = msg });

                    return Ok(new { message = msg, data = new List<object>(), tokensUsed, conversationId = convId });
                }

                string chosenPromptText =
        @"User said:
{{$input}}

Relevant context:
{{$context}}";

                string context = topchunks.Context;
                var finalPrompt = PromptService.PDFAddUserInputBookInPrompt(
                    chosenPromptText,
                    message,
                    context,
                    topchunks.TopBooks
                );

                tokensUsed += EstimateTokens(context) + EstimateTokens(finalPrompt);

                // Build history (session-free)
                var history = await GetLimitedHistoryAsync_up(conversationId: convId, maxHistory: 20, userMessage: finalPrompt, chosenService: chosenService, ct: ct);

                var chat = _kernel.Services.GetRequiredService<IChatCompletionService>();
                var reply = await chat.GetChatMessageContentsAsync(history, cancellationToken: ct);
                var ans = reply.FirstOrDefault()?.Content?.ToString() ?? "(no answer)";
                tokensUsed += EstimateTokens(ans);

                // persist assistant
                if (_chat != null && convId > 0)
                {
                    var asstResp = await _chat.AppendMessageAsync(new AppendMessage
                    {
                        ConversationId = convId,
                        SenderType = "assistant",
                        SenderId = null,
                        Text = ans
                    });
                    asstMsgId = asstResp.Data;

                    await _chat.AddMetadataBulkAsync(asstMsgId, new Dictionary<string, string>
                    {
                        ["mode"] = "text",
                        ["tokensUsed"] = tokensUsed.ToString()
                    });

                    await _chat.IncrementConversationTokensAsync(new IncrementConversationTokens { ConversationId = convId, Tokens = tokensUsed });
                    await _chat.IncrementCompanyTokensAsync(new IncrementCompanyTokens { companyId = companyId, Tokens = tokensUsed });
                }

                // Title update
                replyTextForTitle = ans;
                if (_chat != null && convId > 0)
                    newTitle = await TryUpdateTitleAsync(convId, message, replyTextForTitle, ct);

                return Ok(new
                {
                    message = ans,
                    data = new List<object>(),
                    picke = new { chunks = topchunks.TopBooks.Count() },
                    tokensUsed,
                    conversationId = convId,
                    newTitle
                });
            }

            // ==================== SQLAnalytics ====================
            if (string.IsNullOrWhiteSpace(dbPathSql))
                return Ok(new
                {
                    message = "Schema store not found. Please seed database schema first.",
                    data = new List<object>(),
                    tokensUsed,
                    conversationId = convId
                });
                var store_sql = new SQLiteVectorStore(dbPathSql, _sqliteLogger);
                Console.WriteLine($"SchemaEntries count = {store_sql.CountSchemaRows()}");
                if (sqlGreets == false)
                {
                    if (ContainsForbiddenWriteSql(message, out var badVerb))
                    {

                        await _chat.AppendMessageAsync(new AppendMessage { ConversationId = convId, SenderType = "assistant", SenderId = null, Text = $"{badVerb} operations are not allowed. I can only run safe SELECT queries." });
                        return Ok(new
                        {
                           
                            
                            message = $"{badVerb} operations are not allowed. I can only run safe SELECT queries.",
                            data = new List<object>(),
                            tokensUsed,
                            conversationId = convId
                        });
                    }

                    var topSchemas = store_sql.GetNearest(queryEmb, TopTables).ToList();
                    if (topSchemas.Count == 0)
                        return Ok(new
                        {
                            message = "No schema available in store. Seed schema first.",
                            data = new List<object>(),
                            tokensUsed,
                            conversationId = convId
                        });

                    var selectedColumnsByTable = new Dictionary<string, List<(string Name, string Type)>>(StringComparer.OrdinalIgnoreCase);
                    var selectedSchemaEntries = new List<SchemaEntry>();

                    foreach (var s in topSchemas)
                    {
                        #region removing column filter for temp
                        var columns = store_sql.GetColumnsForTable(s.TableName);
                        var semanticTop = store_sql.GetNearestColumns(queryEmb, s.TableName, columns.Count())
                                                   .Select(x => (Name: x.Entry.ColumnName, Type: x.Entry.ColumnType, Score: x.Score))
                                                   .ToList();

                        var scores = new Dictionary<(string Name, string Type), double>();
                        foreach (var (Name, Type, Score) in semanticTop)
                            if (!string.IsNullOrWhiteSpace(Name))
                                scores[(Name, Type)] = Math.Max(scores.TryGetValue((Name, Type), out var v) ? v : 0, 0.70 * Score);

                        var normQuery = TextSimilarity.Normalize(message);
                        foreach (var c in columns)
                        {
                            var colName = c.ColumnName;
                            var colType = c.ColumnType;
                            if (string.IsNullOrWhiteSpace(colName)) continue;

                            var normCol = TextSimilarity.Normalize(colName);
                            var normBoth = TextSimilarity.Normalize($"{s.TableName} {colName}");
                            var fz1 = TextSimilarity.JaroWinkler(normQuery, normCol);
                            var fz2 = TextSimilarity.JaroWinkler(normQuery, normBoth);
                            var fz = Math.Max(fz1, fz2);

                            scores[(colName, colType)] = (scores.TryGetValue((colName, colType), out var v) ? v : 0) + 0.30 * fz;
                        }

                        var pickedCols = scores.OrderByDescending(kv => kv.Value)
                                               .Take(TopColumnsPerTable)
                                               .Select(kv => kv.Key)
                                               .ToList();

                        selectedColumnsByTable[s.TableName] = pickedCols;
                        #endregion

                        var schemaText = new System.Text.StringBuilder();
                        schemaText.AppendLine($"Table: {s.TableName}");
                        schemaText.AppendLine("Columns:");
                        //schemaText.AppendLine(s.SchemaText);
                        foreach (var c in pickedCols) schemaText.AppendLine($" - {c.Name}  {c.Type}");
                        var emb = (await embedder.GenerateEmbeddingAsync(schemaText.ToString())).ToArray();
                        //var emb = (await embedder.GenerateEmbeddingAsync(s.SchemaText.ToString())).ToArray();
                        selectedSchemaEntries.Add(new SchemaEntry(s.TableName, schemaText.ToString(), emb));
                    }



                    var chosenPrompt = PromptService.sqlUserPrompt;

                    var finalPromptSql = PromptService.AddUserInputInPrompt(chosenPrompt, message, selectedSchemaEntries);
                    Console.WriteLine("Prompt : " + finalPromptSql.ToString());
                    tokensUsed += EstimateTokens(finalPromptSql);

                    var chatSql = _kernel.Services.GetRequiredService<IChatCompletionService>();

                    // Build history (session-free)
                    var historySql = await GetLimitedHistoryAsync_up(conversationId: convId, maxHistory: 20, userMessage: finalPromptSql, chosenService: chosenService, ct: ct);

                    var replySql = await chatSql.GetChatMessageContentsAsync(historySql, cancellationToken: ct);
                    Console.WriteLine("Raw reply: " + replySql.FirstOrDefault()?.Content?.ToString());
                    var answer = replySql.FirstOrDefault()?.Content?.ToString() ?? "(no answer)";

                    var raw = replySql.FirstOrDefault()?.Content?.ToString() ?? string.Empty;
                    if (raw.Contains(("select").ToUpper()) == false && raw.Contains(("select").ToLower()) == false)
                    {
                        if (_chat != null && userMsgId > 0 && !string.IsNullOrWhiteSpace(raw))
                            await _chat.AddMetadataAsync(userMsgId, "Text", raw);

                        tokensUsed += EstimateTokens(raw);

                        if (string.IsNullOrWhiteSpace(raw))
                        {
                            var msg = "Model did not return SQL.";
                            if (_chat != null && convId > 0)
                                await _chat.AppendMessageAsync(new AppendMessage { ConversationId = convId, SenderType = "assistant", SenderId = null, Text = msg });

                            replyTextForTitle = msg;
                            if (_chat != null && convId > 0) newTitle = await TryUpdateTitleAsync(convId, message, replyTextForTitle, ct);

                            return Ok(new
                            {
                                message = msg,
                                data = new List<object>(),
                                columnsPicked = selectedColumnsByTable,
                                tokensUsed,
                                conversationId = convId,
                                newTitle
                            });
                        }

                        return Ok(new
                        {
                            message = raw,
                            data = new List<object>(),
                            tokensUsed,
                            conversationId = convId
                        });
                    }
                    else
                    {
                        var sql = raw.Replace("```sql", "").Replace("```", "").Trim();

                        if (_chat != null && userMsgId > 0 && !string.IsNullOrWhiteSpace(sql))
                            await _chat.AddMetadataAsync(userMsgId, "sql", sql);

                        tokensUsed += EstimateTokens(sql);

                        if (string.IsNullOrWhiteSpace(sql))
                        {
                            var msg = "Model did not return SQL.";
                            if (_chat != null && convId > 0)
                                await _chat.AppendMessageAsync(new AppendMessage { ConversationId = convId, SenderType = "assistant", SenderId = null, Text = msg });

                            replyTextForTitle = msg;
                            if (_chat != null && convId > 0) newTitle = await TryUpdateTitleAsync(convId, message, replyTextForTitle, ct);

                            return Ok(new
                            {
                                message = msg,
                                data = new List<object>(),
                                columnsPicked = selectedColumnsByTable,
                                tokensUsed,
                                conversationId = convId,
                                newTitle
                            });
                        }

                        // Execute SQL (read-only expected)
                        var rows = new List<Dictionary<string, object>>();
                        var table = new DataTable();
                        try
                        {
                           

                            table = DatabaseReading.ExecuteQueryForChat(dbType, connStr, databaseName, sql);

                            foreach (DataRow dr in table.Rows)
                            {
                                var r = new Dictionary<string, object>();
                                foreach (DataColumn c in table.Columns)
                                    r[c.ColumnName] = dr[c];
                                rows.Add(r);
                            }
                        }
                        catch (Exception ex)
                        {
                            var err = "Error executing query: " + ex.Message;
                            var err1 = "Unable answer for now "; //+ex.Message
                            if (_chat != null && convId > 0)
                                await _chat.AppendMessageAsync(new AppendMessage { ConversationId = convId, SenderType = "assistant", SenderId = null, Text = err });

                            replyTextForTitle = err;
                            if (_chat != null && convId > 0) newTitle = await TryUpdateTitleAsync(convId, message, replyTextForTitle, ct);

                            return Ok(new
                            {
                                message = err1,
                                data = new List<object>(),
                                columnsPicked = selectedColumnsByTable,
                                tokensUsed,
                                conversationId = convId,
                                newTitle
                            });
                        }

                        // Chart branch
                        if (wantsChart && table != null && table.Columns.Count > 0 && table.Rows.Count > 0)
                        {
                            var (specJson, chartSpecTokens) = await GenerateChartSpecAsync(table, message, ct);
                            tokensUsed += chartSpecTokens;

                            if (_chat != null && convId > 0)
                            {
                                var chartDataJson = System.Text.Json.JsonSerializer.Serialize(DataTableToPlain(table));

                                // build a single compact payload for replay
                                var chartPayload = new
                                {
                                    mode = "chart",
                                    // store spec as JSON object (not string) if you can; otherwise keep string
                                    spec = System.Text.Json.JsonSerializer.Deserialize<object>(specJson),
                                    data = DataTableToPlain(table)  // your helper -> Chart.js-ready data or your own shape
                                };
                                var chartJsonForMessage = System.Text.Json.JsonSerializer.Serialize(chartPayload);


                                var asstResp2 = await _chat.AppendMessageAsync(new AppendMessage
                                {
                                    ConversationId = convId,
                                    SenderType = "assistant",
                                    SenderId = null,
                                    Text = sql,//"[chart result]",
                                    Json = chartJsonForMessage
                                });
                                asstMsgId = asstResp2.Data;

                                var colJson = System.Text.Json.JsonSerializer.Serialize(table.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
                                await _chat.AddMetadataBulkAsync(asstMsgId, new Dictionary<string, string>
                                {
                                    ["mode"] = "chart",
                                    ["rowCount"] = table.Rows.Count.ToString(),
                                    ["columns"] = colJson,
                                    ["chartSpec"] = specJson,
                                    ["chartData"] = chartDataJson,
                                    ["tokensUsed"] = tokensUsed.ToString()
                                });

                                await _chat.IncrementConversationTokensAsync(new IncrementConversationTokens { ConversationId = convId, Tokens = tokensUsed });
                                await _chat.IncrementCompanyTokensAsync(new IncrementCompanyTokens { companyId = companyId, Tokens = tokensUsed });
                            }

                            replyTextForTitle = $"Chart for {table.Rows.Count} rows";
                            if (_chat != null && convId > 0) newTitle = await TryUpdateTitleAsync(convId, message, replyTextForTitle, ct);

                            return Ok(new
                            {
                                message = "Here’s a chart for your result.",
                                data = rows,
                                chart = new { spec = specJson, data = DataTableToPlain(table) },
                                tokensUsed,
                                conversationId = convId,
                                newTitle
                            });
                        }

                        // Table / plain result
                        var okMsg = rows.Count == 0 ? "No data found." : "Here are your results:";
                        if (_chat != null && convId > 0)
                        {
                            // serialize table rows + columns
                            var rowsJson = System.Text.Json.JsonSerializer.Serialize(rows);

                            var tablePayload = new
                            {
                                mode = "table",
                                data = new
                                {
                                    columns = table.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToArray(),
                                    rows = table.Rows.Cast<DataRow>().Select(r =>
                                        table.Columns.Cast<DataColumn>().Select(c => r[c]).ToArray()
                        ).ToArray()
                                }
                            };
                            var tableJsonForMessage = System.Text.Json.JsonSerializer.Serialize(tablePayload);


                            var asstResp3 = await _chat.AppendMessageAsync(new AppendMessage
                            {
                                ConversationId = convId,
                                SenderType = "assistant",
                                SenderId = null,
                                Text = sql, //rows.Count == 0 ? okMsg : "[table result]",
                                Json = tableJsonForMessage        // <<< IMPORTANT
                            });
                            asstMsgId = asstResp3.Data;

                            var colJson = System.Text.Json.JsonSerializer.Serialize(table.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
                            await _chat.AddMetadataBulkAsync(asstMsgId, new Dictionary<string, string>
                            {
                                //    ["mode"] = "table",
                                //    ["rowCount"] = table.Rows.Count.ToString(),
                                //    ["columns"] = colJson,
                                //    ["tokensUsed"] = tokensUsed.ToString()

                                ["mode"] = "table",
                                ["rowCount"] = table.Rows.Count.ToString(),
                                ["columns"] = colJson,
                                ["tableData"] = rowsJson,            // <—— add this
                                ["tokensUsed"] = tokensUsed.ToString()
                            });

                            await _chat.IncrementConversationTokensAsync(new IncrementConversationTokens { ConversationId = convId, Tokens = tokensUsed });
                            await _chat.IncrementCompanyTokensAsync(new IncrementCompanyTokens { companyId = companyId, Tokens = tokensUsed });
                        }

                        replyTextForTitle = rows.Count == 0 ? okMsg :
                            $"Table result: {rows.Count} rows from {string.Join(", ", selectedColumnsByTable.Keys.Take(2))}{(selectedColumnsByTable.Count > 2 ? "…" : "")}";
                        if (_chat != null && convId > 0) newTitle = await TryUpdateTitleAsync(convId, message, replyTextForTitle, ct);



                        return Ok(new
                        {
                            message = okMsg,
                            data = rows,
                            columnsPicked = selectedColumnsByTable,
                            tokensUsed,
                            conversationId = convId,
                            newTitle
                        });
                    }
                }
                else
                {  
                    var chosenPrompt = PromptService.sqlUserPrompt;

                    var finalPromptSql = PromptService.AddUserInputInPrompt(chosenPrompt, message, new List<SchemaEntry>());
                    Console.WriteLine("Prompt : " + finalPromptSql.ToString());
                    tokensUsed += EstimateTokens(finalPromptSql);

                    var chatSql = _kernel.Services.GetRequiredService<IChatCompletionService>();

                    // Build history (session-free)
                    var historySql = await GetLimitedHistoryAsync_up(conversationId: convId, maxHistory: 20, userMessage: finalPromptSql, chosenService: chosenService, ct: ct);

                    var replySql = await chatSql.GetChatMessageContentsAsync(historySql, cancellationToken: ct);
                    Console.WriteLine("Raw reply: " + replySql.FirstOrDefault()?.Content?.ToString());
                    var answer = replySql.FirstOrDefault()?.Content?.ToString() ?? "(no answer)";

                    var raw = replySql.FirstOrDefault()?.Content?.ToString() ?? string.Empty;
                    //var sql = raw.Replace("```sql", "").Replace("```", "").Trim();

                    if (_chat != null && userMsgId > 0 && !string.IsNullOrWhiteSpace(raw))
                        await _chat.AddMetadataAsync(userMsgId, "Reply", raw);

                    tokensUsed += EstimateTokens(raw);

                    if (string.IsNullOrWhiteSpace(raw))
                    {
                        var msg = "Model did not return SQL.";
                        if (_chat != null && convId > 0)
                            await _chat.AppendMessageAsync(new AppendMessage { ConversationId = convId, SenderType = "assistant", SenderId = null, Text = msg });

                        replyTextForTitle = msg;
                        if (_chat != null && convId > 0) newTitle = await TryUpdateTitleAsync(convId, message, replyTextForTitle, ct);

                        return Ok(new
                        {
                            message = msg,
                            data = new List<object>(),
                            //columnsPicked = new List(),
                            tokensUsed,
                            conversationId = convId,
                            newTitle
                        });
                    }

                   

                    // Table / plain result
                    var okMsg = raw;
                    if (_chat != null && convId > 0)
                    {
                        


                        var asstResp4 = await _chat.AppendMessageAsync(new AppendMessage
                        {
                            ConversationId = convId,
                            SenderType = "assistant",
                            SenderId = null,
                            Text = raw, //rows.Count == 0 ? okMsg : "[table result]",
                            Json = null        // <<< IMPORTANT
                        });
                        asstMsgId = asstResp4.Data;

                        //var colJson = System.Text.Json.JsonSerializer.Serialize(table.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
                        await _chat.AddMetadataBulkAsync(asstMsgId, new Dictionary<string, string>
                        {
                            //    ["mode"] = "table",
                            //    ["rowCount"] = table.Rows.Count.ToString(),
                            //    ["columns"] = colJson,
                            //    ["tokensUsed"] = tokensUsed.ToString()

                            ["mode"] = "Text",
                            ["Reply"] = raw,
                            ["tokensUsed"] = tokensUsed.ToString()
                        });

                        await _chat.IncrementConversationTokensAsync(new IncrementConversationTokens { ConversationId = convId, Tokens = tokensUsed });
                        await _chat.IncrementCompanyTokensAsync(new IncrementCompanyTokens { companyId = companyId, Tokens = tokensUsed });
                    }

                    replyTextForTitle =  okMsg ;
                    if (_chat != null && convId > 0) newTitle = await TryUpdateTitleAsync(convId, message, replyTextForTitle, ct);



                    return Ok(new
                    {
                        message = okMsg,
                        //data = rows,
                        //columnsPicked = selectedColumnsByTable,
                        tokensUsed,
                        conversationId = convId,
                        newTitle
                    });
                }
            }
            catch (Exception ex)
            {
                // ERROR Log: Chat exception
                _logger.LogError(
                    ex,
                    "Error in AskChat_up API - User: {Username}, Service: {Service}, Message: {Message}, CorrelationId: {CorrelationId}, Error: {ErrorMessage}",
                    username, service, message, correlationId, ex.Message);

                // SECURITY Log: Chat error
                _auditLogger.LogSecurityEvent(
                    "CHAT_PROCESSING_ERROR",
                    username,
                    ipAddress,
                    $"Error in chat processing: {ex.Message}, CorrelationId: {correlationId}");

                throw;
            }
        }
        // Strip comments + collapse whitespace so "DELETE   FROM" or "-- stuff" doesn't bypass
        private static string PreprocessSqlText(string s)
        {
            s = Regex.Replace(s, @"/\*.*?\*/", " ", RegexOptions.Singleline); // /* ... */
            s = Regex.Replace(s, @"--.*?$", " ", RegexOptions.Multiline);     // -- to EOL
            s = Regex.Replace(s, @"\s+", " ");                                // collapse spaces
            return s.Trim();
        }

        // Phrases like "don't delete", "without dropping" shouldn't trigger a block
        private static readonly Regex NegatedWriteOps = new(
            @"\b(?:don't|do\s+not|without|never)\s+(?:delete|update|insert|drop)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        // Detect DELETE/UPDATE/INSERT/DROP as actual SQL-intent (not casual English)
        private static readonly Regex ForbiddenWriteOps = new(
            // DELETE FROM … | UPDATE <obj> SET … | INSERT [INTO] <obj> ( … | VALUES … ) | DROP (TABLE|VIEW|…)
            //@"(?<!\w)(?:delete\s+from|update\s+[\[\]""\w\.]+\s+set|insert\s+(?:into\s+)?[\[\]""\w\.]+\s*(?:\(|values\b)|drop\s+(?:table|database|view|index|procedure|function|schema|trigger))\b",
            @"\b(?:delete|update|insert|drop)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

        private static bool ContainsForbiddenWriteSql(string? text, out string? verb)
        {
            verb = null;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var t = PreprocessSqlText(text);
            if (NegatedWriteOps.IsMatch(t)) return false; // user said "don't delete" etc.

            var m = ForbiddenWriteOps.Match(t);
            if (!m.Success) return false;

            if (Regex.IsMatch(m.Value, @"\bdelete\b", RegexOptions.IgnoreCase)) verb = "DELETE";
            else if (Regex.IsMatch(m.Value, @"\bupdate\b", RegexOptions.IgnoreCase)) verb = "UPDATE";
            else if (Regex.IsMatch(m.Value, @"\binsert\b", RegexOptions.IgnoreCase)) verb = "INSERT";
            else if (Regex.IsMatch(m.Value, @"\bdrop\b", RegexOptions.IgnoreCase)) verb = "DROP";

            return true;
        }

        // very cheap greeting detector (English + a few common Indian greetings/abbrevs)
        private static readonly Regex GreetingRegex = new(
            @"\b(hi+|hello+|hey+|howdy|yo|hola|namaste|namaskar|salaam|salam|vanakkam|kem\s*cho|sat\s*sri\s*akaal|"
          + @"good\s*(morning|afternoon|evening|night)|gm|gn|how\s*are\s*you\??)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static bool IsGreeting(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            var t = text.Trim();

            // avoid classic false-positive
            if (t.Contains("hello world", StringComparison.OrdinalIgnoreCase)) return false;

            // treat short messages that match greeting patterns as greetings
            var wordCount = Regex.Matches(t, @"\p{L}+").Count;
            return GreetingRegex.IsMatch(t) && (t.Length <= 40 || wordCount <= 6);
        }

        private static string StripSqlFence(string raw)
    => raw.Replace("```sql", "", StringComparison.OrdinalIgnoreCase)
          .Replace("```", "", StringComparison.OrdinalIgnoreCase)
          .Trim();

        private  string GetPrompt(int companyId,string chosenServices)
        {
            var svc = (!string.IsNullOrEmpty(chosenServices) && chosenServices == "SQLAnalytics") ? 1 : 2;
            var strData =  _repo.GetPromptCompany(new GetPromptCompanyId { CompanyId = companyId, SvcType = svc });
            return strData.Result.Data;
        }

        private async Task<ChatHistory> GetLimitedHistoryAsync_up(
    long conversationId,
    int maxHistory,
    string? userMessage = null,
    string? assistantMessage = null,
    string? chosenService = "SQLAnalytics",
    CancellationToken ct = default)
        {
            // Build a fresh in-memory history every time (no Session).
            var chatHistory = new ChatHistory();

            var sysPrompt = GetPrompt(Convert.ToInt32(User.FindFirstValue("CompanyId")), chosenService);
            // Exactly one governing system prompt for the LLM context.
            var systemPrompt = string.Equals(chosenService, "FileAnalytics", StringComparison.OrdinalIgnoreCase)
                ? sysPrompt+"/n/n"+ " You are a helpful assistant. Use prior messages as context." + "/n/n" + PromptService.Pdfrule
                : sysPrompt+"/n/n"+ PromptService.Rules;

            chatHistory.AddSystemMessage(systemPrompt);

            // Pull last N messages for THIS conversation from your chat store.
            if (_chat != null && conversationId > 0)
            {
                var resp = await _chat.GetConversationMessagesAsync(
                    new GetConversationMessages { ConversationId = conversationId });

                var msgs = resp?.Data ?? new List<ChatMessageVm>();

                // Keep only the most recent 'maxHistory' messages.
                foreach (var m in msgs.Skip(Math.Max(0, msgs.Count - maxHistory)))
                {
                    var sender = (m.SenderType ?? "").Trim().ToLowerInvariant();
                    var text = m.MessageText ?? string.Empty;

                    // Only add user/assistant to LLM context (skip non-rule system lines like "service=...").
                    if (sender == "user") chatHistory.AddUserMessage(text);
                    else if (sender == "assistant") chatHistory.AddAssistantMessage(text);
                }
            }

            // Optionally append transient messages for this turn.
            if (!string.IsNullOrWhiteSpace(userMessage)) chatHistory.AddUserMessage(userMessage);
            if (!string.IsNullOrWhiteSpace(assistantMessage)) chatHistory.AddAssistantMessage(assistantMessage);

            return chatHistory;
        }

        private ChatHistory GetLimitedHistory(long conversationId, int maxHistory, string? userMessage = null, string? assistantMessage = null,string? chosenService= "SQLAnalytics")
        {
            var sessionKey = "ChatHistory";
            var resp = _chat.GetConversationMessagesAsync(new GetConversationMessages { ConversationId = conversationId })
   .GetAwaiter().GetResult();
            
            
            var historyJson = HttpContext.Session.GetString(sessionKey);
            var chatHistory = //string.IsNullOrEmpty(historyJson)
                (resp?.Data?.Count()==0) ? new ChatHistory()
                : System.Text.Json.JsonSerializer.Deserialize<ChatHistory>(historyJson) ?? new ChatHistory();

            string systemPrompt = (string.Equals(chosenService, "FileAnalytics", StringComparison.OrdinalIgnoreCase)) ? "You are a helpful assistant. Use prior messages as context." : PromptService.Rules;

            if (chatHistory.Count == 0 ||
                chatHistory[0] is not { Role.Label: "system" } ||
                !chatHistory[0].Items.OfType<TextContent>().Any(t => t.Text == systemPrompt))
            {

                chatHistory.AddSystemMessage(systemPrompt);
            }

            if (_chat != null && conversationId > 0 && (resp?.Data?.Count()>0))
            {
                //var resp = _chat.GetConversationMessagesAsync(new GetConversationMessages { ConversationId = conversationId }).GetAwaiter().GetResult();
                var msgs = resp?.Data ?? new List<ChatMessageVm>();

                foreach (var m in msgs.Skip(Math.Max(0, msgs.Count - maxHistory)))
                {
                    var sender = (m.SenderType ?? "").Trim().ToLowerInvariant();
                    var text = m.MessageText ?? string.Empty;

                    if (sender == "user") chatHistory.AddUserMessage(text);
                    else if (sender == "assistant") chatHistory.AddAssistantMessage(text);
                    else if (sender == "system") chatHistory.AddSystemMessage(text);
                    else chatHistory.AddSystemMessage($"[{m.SenderType}] {text}");
                }
            }

            if (!string.IsNullOrWhiteSpace(userMessage)) chatHistory.AddUserMessage(userMessage);
            if (!string.IsNullOrWhiteSpace(assistantMessage)) chatHistory.AddAssistantMessage(assistantMessage);

            HttpContext.Session.SetString(sessionKey,
                System.Text.Json.JsonSerializer.Serialize(chatHistory));

            return chatHistory;
            
            

            
        }
        
       
        private async Task<string?> TryUpdateTitleAsync(long convId, string userText, string replyText, CancellationToken ct)
        {
            try
            {
                var oldT = getTitleAsync(convId, ct);
                string? newTitle=null;
                if (oldT != null && oldT == "New chat")
                {
                    newTitle = await GenerateShortTitleAsync(userText, replyText, ct);
                    await _chat.UpdateConversationTitleAsync(new UpdateConversationTitle
                    {
                        ConversationId = convId,
                        Title = newTitle
                    });
                }
                else
                {
                    newTitle = oldT;
                }

                //var newTitle = await GenerateShortTitleAsync(userText, replyText, ct);
                if (!string.IsNullOrWhiteSpace(newTitle))
                {
                    //await _chat.UpdateConversationTitleAsync(new UpdateConversationTitle
                    //{
                    //    ConversationId = convId,
                    //    Title = newTitle
                    //});
                    return newTitle;
                }
            }
            catch { /* ignore title errors */ }
            return null;
        }

        private string getTitleAsync(long convId,CancellationToken ct)
        {
            string OldTitle = "";
            try
            {
                if(_chat!=null)
                {
                    //OldTitle =_chat.GetConversationTitleAsync(new GetConversationMessages { ConversationId=convId}).Result.Data.Title;
                   return _chat.GetConversationTitleAsync(new GetConversationMessages { ConversationId=convId}).Result.Data.Title;
                   // return OldTitle;
                }
               
               
            }
            catch { /* ignore title errors */ }
            return null;
        }

        private async Task<string> GenerateShortTitleAsync(string userText, string replyText, CancellationToken ct)
        {
            // Small prompt to keep cost low
            var sys = "You create concise chat titles (6–40 chars). No quotes. No trailing punctuation.";

            var chatSvc = _kernel.Services.GetRequiredService<IChatCompletionService>();

            // Build ChatHistory (required by SK)
            var history = new ChatHistory();
            history.AddSystemMessage(sys);
            history.AddUserMessage($"User: {userText}");
            history.AddAssistantMessage($"Assistant: {replyText}");
            history.AddUserMessage("Title:"); // ask for just the title

            var res = await chatSvc.GetChatMessageContentsAsync(history, cancellationToken: ct);
            var title = (res.FirstOrDefault()?.Content ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(title))
                title = (userText.Length <= 40 ? userText : userText[..40] + "…").Trim();

            // sanitize a bit
            title = title.Replace("\r", " ").Replace("\n", " ").Trim().Trim('"', '\'', '`', '.', ',');
            if (title.Length > 60) title = title[..60].Trim();

            return title;
        }


        private static object DataTableToPlain(DataTable dt)
        {
            var cols = dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
            var rows = new List<object[]>();
            foreach (DataRow r in dt.Rows)
            {
                var arr = new object[cols.Count];
                for (int i = 0; i < cols.Count; i++) arr[i] = r[cols[i]];
                rows.Add(arr);
            }
            return new { columns = cols, rows };
        }


        private async Task<(string specJson, int tokens)> GenerateChartSpecAsync(DataTable table, string message, CancellationToken ct)
        {
            // Summarize schema + first 20 rows (don’t send full data)
            var schema = table.Columns.Cast<DataColumn>()
                .Select(c => new { name = c.ColumnName, type = c.DataType.Name })
                .ToList();

            var sample = new List<Dictionary<string, object?>>();
            int take = Math.Min(table.Rows.Count, 20);
            for (int i = 0; i < take; i++)
            {
                var dr = table.Rows[i];
                var row = new Dictionary<string, object?>();
                foreach (DataColumn c in table.Columns) row[c.ColumnName] = dr[c];
                sample.Add(row);
            }

            // Instruction for strict Chart.js spec
            var instruction = @"
You will receive a table schema and sample rows in JSON.
Respond with a STRICT JSON object for Chart.js with fields:
- type: one of bar|line|pie|doughnut|scatter
- xKey: column name for X axis (prefer date/time or category)
- yKeys: array of 1-3 numeric column names
- title: short chart title
- Do not use markdown formatting or triple backticks.
Return ONLY the JSON (no prose). Choose sensible defaults.";

            //var payload = JsonSerializer.Serialize(new { schema, sample });
            var payload = System.Text.Json.JsonSerializer.Serialize(new { schema, sample });

            // Use your existing SK chat service
            var chat = _kernel.Services.GetRequiredService<IChatCompletionService>();
            //var history = new ChatHistory();
            //var history = oldHis;
            //history.AddSystemMessage(instruction);
            //history.AddUserMessage(payload);

            var nprompt = "User request: " + message + "\n" + instruction + "\n" + payload;

            var reply = await chat.GetChatMessageContentsAsync(nprompt, cancellationToken: ct);

            var spec = reply.FirstOrDefault()?.Content?.ToString()?.Trim();
            Console.WriteLine($"Chart spec: {spec}");

            if (string.IsNullOrWhiteSpace(spec))
            {
                // Safe fallback: basic bar with first string as X and first numeric as Y
                var cols = table.Columns.Cast<DataColumn>().ToList();
                var x = cols.FirstOrDefault(c => c.DataType == typeof(string))?.ColumnName ?? cols.First().ColumnName;
                var y = cols.FirstOrDefault(c =>
                            c.DataType == typeof(int) || c.DataType == typeof(long) ||
                            c.DataType == typeof(float) || c.DataType == typeof(double) ||
                            c.DataType == typeof(decimal))?.ColumnName
                        ?? cols.Last().ColumnName;

                //spec = JsonSerializer.Serialize(new { type = "bar", xKey = x, yKeys = new[] { y }, title = "Chart" });
                spec = System.Text.Json.JsonSerializer.Serialize(new { type = "bar", xKey = x, yKeys = new[] { y }, title = "Chart" });
            }

            // If you don’t have token usage here, estimate by length; you can wire exact usage later.
            int estTokens = (instruction.Length + payload.Length + spec.Length) / 4;
            return (spec, estTokens);
        }

        
        [HttpGet("Services")]
        public IActionResult Services([FromQuery] int companyId)
        {
            var sqlDb = User.FindFirstValue("SQLitedbName");
            var fileDb = User.FindFirstValue("SQLitedbName") ?? sqlDb;


            return Ok(new { hasSql = !string.IsNullOrWhiteSpace(sqlDb), hasFile = !string.IsNullOrWhiteSpace(fileDb) });
        }

       

        [HttpGet("History")]
        public async Task<IActionResult> History([FromQuery] int? userId)
        {
            if (_chat == null) return Ok(Array.Empty<object>());

            var uid = userId.GetValueOrDefault();

            // Prefer claims
            if (uid <= 0 && User?.Identity?.IsAuthenticated == true)
            {
                var claim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (claim != null && int.TryParse(claim.Value, out var parsed)) uid = parsed;
            }

            // Fallback to session (your login code stores this)
            if (uid <= 0)
                uid = HttpContext.Session.GetInt32("UserId") ?? 0;

            if (uid <= 0) return Ok(Array.Empty<object>()); // not logged in

            var list = await _chat.GetRecentConversationsAsync(new GetRecentConversations { UserId = uid, Take = 5 });
            return Ok(list);
        }

        [HttpGet("History/me")]
        public async Task<IActionResult> HistoryMe()
        {
            if (_chat == null) return Ok(Array.Empty<object>());

            var uid = 0;
            if (User?.Identity?.IsAuthenticated == true)
            {
                var claim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (claim != null && int.TryParse(claim.Value, out var parsed)) uid = parsed;
            }
            if (uid <= 0)
                uid = HttpContext.Session.GetInt32("UserId") ?? 0;

            if (uid <= 0) return Ok(Array.Empty<object>());

            var list = await _chat.GetRecentConversationsAsync(new GetRecentConversations { UserId = uid, Take = 5 });
            return Ok(list);
        }



        // full message list for a conversation
        [HttpGet("Messages/{conversationId:long}")]
        public async Task<IActionResult> Messages([FromRoute] long conversationId)
        {
            if (_chat == null) return Ok(Array.Empty<object>());
            var list = await _chat.GetConversationMessagesAsync(new GetConversationMessages { ConversationId = conversationId });
            return Ok(list);
        }

        // create a new conversation and log the chosen service
        public sealed class StartChatDto { public int CompanyId { get; set; } public int UserId { get; set; } public string? Service { get; set; } }
        [HttpPost("Start")]
        public async Task<IActionResult> Start([FromBody] StartChatDto req)
        {
            if (_chat == null) return Ok(new { conversationId = (long?)null });

            var sessionKey = "ChatHistory";
            var chatHistory = new ChatHistory();
            HttpContext.Session.SetString(sessionKey,
              System.Text.Json.JsonSerializer.Serialize(chatHistory));


            var historyJson = HttpContext.Session.GetString(sessionKey);

            var uid = req.UserId;
            if (User?.Identity?.IsAuthenticated == true)
            {
                var claim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (claim != null && int.TryParse(claim.Value, out var parsed)) uid = parsed;
            }

            var title = "New chat";
            var result = await _chat.StartConversationAsync(new StartConversation { UserId = uid, Title = title });
            var convId = result.Data;
            await _chat.AppendMessageAsync(new AppendMessage { ConversationId = convId, SenderType = "assistant", SenderId = null, Text = $"service={req.Service ?? "SQLAnalytics"}" });
            return Ok(new { conversationId = convId });
        }

        [HttpPost("Start1")]
        public async Task<IActionResult> Start_up([FromBody] StartChatDto req)
        {
            if (_chat == null) return Ok(new { conversationId = (long?)null });

            // Resolve the user id (prefer authenticated user over payload)
            var uid = req.UserId;
            if (User?.Identity?.IsAuthenticated == true)
            {
                var claim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (claim != null && int.TryParse(claim.Value, out var parsed)) uid = parsed;
            }

            // Always create a brand-new conversation (no Session involvement)
            var title = "New chat";
            var startResp = await _chat.StartConversationAsync(
                new StartConversation { UserId = uid, Title = title });
            var convId = startResp.Data;

            // Pick service and seed exactly ONE system message for the LLM context
            var chosenService = string.IsNullOrWhiteSpace(req.Service) ? "SQLAnalytics" : req.Service;

            var sysPrompt = GetPrompt(Convert.ToInt32(User.FindFirstValue("CompanyId")), chosenService);
            // Exactly one governing system prompt for the LLM context.
            var rules = string.Equals(chosenService, "FileAnalytics", StringComparison.OrdinalIgnoreCase)
                ? sysPrompt + "/n/n" + " You are a helpful assistant. Use prior messages as context." + "/n/n" + PromptService.Pdfrule
                : sysPrompt + "/n/n" + PromptService.Rules;

            //var rules = string.Equals(chosenService, "FileAnalytics", StringComparison.OrdinalIgnoreCase)
            //    ? "You are a helpful assistant. Use prior messages as context."
            //    : PromptService.Rules;

            // Keep audit of the selected service inline without creating a 2nd system row
            var initialSystem = $"{rules}\n\n[service={chosenService}]";

            await _chat.AppendMessageAsync(new AppendMessage
            {
                ConversationId = convId,
                SenderType = "system",
                SenderId = null,
                Text = initialSystem
            });

            return Ok(new { conversationId = convId });
        }

        private static string SanitizeSql(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql)) return sql ?? string.Empty;

            // remove``` fences if any
            sql = StripSqlFence(sql);

            // remove GO batch separators
            sql = Regex.Replace(sql, @"(?mi)^\s*GO\s*;?\s*$", "", RegexOptions.Multiline);

            // remove single-line comments
            sql = Regex.Replace(sql, @"--.*?$", "", RegexOptions.Multiline);

            // keep first statement only
            var idx = sql.IndexOf(';');
            if (idx >= 0) sql = sql[..idx];

            return sql.Trim();
        }


        
        [HttpGet("{id:int}")]
        public IActionResult GetById([FromRoute] int id)
        {
            var username = User?.Identity?.Name ?? "anonymous";
            var correlationId = HttpContext.TraceIdentifier;

            // INFO Log: Stub method called
            _logger.LogInformation(
                "GetById stub called - CompanyId: {CompanyId}, User: {Username}, CorrelationId: {CorrelationId}",
                id, username, correlationId);

            // Implement your own fetch if you want; for now return 501
            return StatusCode(501, new { message = "Not implemented (sample stub)." });
        }

        #endregion commented

        //suchita
        [HttpGet("GetAllCompanyList")]
        [SessionAuthorize]
        public async Task<IActionResult> GetAllCompanyList()
        {
            var username = User?.Identity?.Name ?? "anonymous";
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var correlationId = HttpContext.TraceIdentifier;

            try
            {
                // INFO Log: Action started
                _logger.LogInformation(
                    "GetAllCompanyList API called - User: {Username}, CorrelationId: {CorrelationId}",
                    username, correlationId);

                var list = await _repo.GetAllCompanyList();

                _logger.LogInformation(
                    "GetAllCompanyList completed - Count: {CompanyCount}, User: {Username}, CorrelationId: {CorrelationId}",
                    list?.Count() ?? 0, username, correlationId);

                // AUDIT Log: Data access
                _auditLogger.LogDataAccess(
                    username,
                    "READ",
                    "CompanyList",
                    "ALL",
                    $"Retrieved all company lists, CorrelationId: {correlationId}");

                return Ok(list);
            }
            catch (Exception ex)
            {
                // ERROR Log: Exception
                _logger.LogError(
                    ex,
                    "Error in GetAllCompanyList API - User: {Username}, CorrelationId: {CorrelationId}, Error: {ErrorMessage}",
                    username, correlationId, ex.Message);

                throw;
            }
        }

        // Update
        [HttpPost("UpdateCompanyList")]
        [SessionAuthorize]
        public async Task<IActionResult> UpdateCompanyList([FromBody] CompanyListUpdate model)
        {
            var username = User?.Identity?.Name ?? "anonymous";
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var correlationId = HttpContext.TraceIdentifier;

            try
            {
                // INFO Log: Action started
                _logger.LogInformation(
                    "UpdateCompanyList API called - User: {Username}, CompanyId: {CompanyId}, CorrelationId: {CorrelationId}",
                    username, model?.CompanyID, correlationId);

                if (User?.Identity?.IsAuthenticated == true)
            {
                // read int userId from claims
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    model.UpdatedBy = userId;

                        _logger.LogDebug(
                          "User ID {UserId} extracted from claims for UpdateCompanyList - CorrelationId: {CorrelationId}",
                          userId, correlationId);
                    }
                else
                {
                    model.UpdatedBy = 0;
                }
            }
            else
            {
                model.UpdatedBy = 0;
            }

            var spResult = await _repo.UpdateCompanyList(model);

            //Nidhi work
            if (spResult.Status == true && spResult.Data > 0)
            {
                    // INFO Log: Success
                    _logger.LogInformation(
                        "Company list updated successfully - CompanyId: {CompanyId}, User: {Username}, CorrelationId: {CorrelationId}",
                        spResult.Data, username, correlationId);

                    // AUDIT Log: Company update
                    _auditLogger.LogDataAccess(
                        username,
                        "UPDATE",
                        "CompanyList",
                        spResult.Data.ToString(),
                        $"Company list updated, CorrelationId: {correlationId}");


                    // Created 201 + payload with new ID
                    return CreatedAtAction(nameof(GetById), new { id = spResult.Data },
                    new { message = spResult.Message, companyId = spResult.Data });
            }

                // WARNING Log: Business logic failure
                _logger.LogWarning(
                    "Company list update failed - Message: {Message}, User: {Username}, CorrelationId: {CorrelationId}",
                    spResult.Message, username, correlationId);

                // Duplicate or other business error from SP
                if (spResult.Message?.Contains("already exists", System.StringComparison.OrdinalIgnoreCase) == true)
                return Conflict(new { message = spResult.Message });

            return BadRequest(new { message = spResult.Message ?? "Insert failed." });
            }
            catch (Exception ex)
            {
                // ERROR Log: Exception
                _logger.LogError(
                    ex,
                    "Error in UpdateCompanyList API - User: {Username}, CompanyId: {CompanyId}, CorrelationId: {CorrelationId}, Error: {ErrorMessage}",
                    username, model?.CompanyID, correlationId, ex.Message);

                throw;
            }
        }




        // Delete
        [HttpDelete("DeleteCompanyList")]
        [SessionAuthorize]
        public async Task<IActionResult> DeleteCompanyList(DeleteCompanyList dtodelete)
        {
            var username = User?.Identity?.Name ?? "anonymous";
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var correlationId = HttpContext.TraceIdentifier;

            try
            {
                // INFO Log: Action started
                _logger.LogInformation(
                    "DeleteCompanyList API called - User: {Username}, CompanyId: {CompanyId}, CorrelationId: {CorrelationId}",
                    username, dtodelete?.CompanyID, correlationId);

                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                dtodelete.DeletedBy = userId;

                    _logger.LogDebug(
                        "User ID {UserId} extracted from claims for DeleteCompanyList - CorrelationId: {CorrelationId}",
                        userId, correlationId);
                }
            else
            {
                dtodelete.DeletedBy = 0;
            }

            var response = await _repo.DeleteCompanyList(dtodelete).ConfigureAwait(false);

                _logger.LogInformation(
                    "DeleteCompanyList completed - Status: {Status}, Message: {Message}, User: {Username}, CorrelationId: {CorrelationId}",
                    response.Status, response.Message, username, correlationId);

                // AUDIT Log: Company deletion
                _auditLogger.LogDataAccess(
                    username,
                    "DELETE",
                    "CompanyList",
                    dtodelete?.CompanyID.ToString() ?? "unknown",
                    $"Company list deleted, CorrelationId: {correlationId}");

                return Ok(response);
            }
            catch (Exception ex)
            {
                // ERROR Log: Exception
                _logger.LogError(
                    ex,
                    "Error in DeleteCompanyList API - User: {Username}, CompanyId: {CompanyId}, CorrelationId: {CorrelationId}, Error: {ErrorMessage}",
                    username, dtodelete?.CompanyID, correlationId, ex.Message);

                throw;
            }

        }


        

        [HttpPost("GetByIdCompanyList")]
        [SessionAuthorize]
        public async Task<IActionResult> GetByIdGetByIdCompanyList([FromBody] GetByIdCompanyList GetByIdCompanyList)
        {
            var username = User?.Identity?.Name ?? "anonymous";
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var correlationId = HttpContext.TraceIdentifier;
            try
            {
                var result = await _repo.GetByIdCompanyList(GetByIdCompanyList).ConfigureAwait(false);
                return Ok(result);
            }
            catch (Exception ex)
            {

                // ERROR Log: Exception
                _logger.LogError(
                    ex,
                    "Error in GetByIdCompanyList API - CorrelationId: {CorrelationId}, " +
                    "Request: {@Request}, " +
                    "Exception Details: {ExceptionType} - {ExceptionMessage} " +
                    "Stack Trace: {StackTrace}",
                    correlationId, ex.Message);

                throw;
                

            }
        }

       


        [HttpPost("UpdateSQLAnalyticsConfiguration")]
        [SessionAuthorize]
        public async Task<IActionResult> UpdateSQLAnalyticsConfiguration([FromBody] SQLAnalyticsUpdate model)
        {
            var correlationId = HttpContext.TraceIdentifier;
            var username = User?.Identity?.Name ?? "anonymous";
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            try
            {
                // 🔹 Identify user (UpdatedBy)
                if (User?.Identity?.IsAuthenticated == true)
                {
                    var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                    if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                        model.UpdatedBy = userId;
                    else
                        model.UpdatedBy = 0;
                }
                else
                {
                    model.UpdatedBy = 0;
                }

                // 🔹 Call repository method
                var response = await _repo.UpdateSQLAnalyticsConfiguration(model);

                // 🔹 Response handling
                if (response.Status)
                {
                    return StatusCode(StatusCodes.Status201Created, response);
                }
                else if (!response.Status)
                {
                    return StatusCode(StatusCodes.Status409Conflict, response);
                }
                else
                {
                    return StatusCode(StatusCodes.Status400BadRequest,
                        new { message = "Something went wrong, please contact system administrator." });
                }
            }
            catch (Exception ex)
            {
                // 🔹 ERROR Logging only (no change to logic)
                _logger.LogError(
                    ex,
                    "Error in UpdateSQLAnalyticsConfiguration API - CorrelationId: {CorrelationId}, " +
                    "User: {Username}, IP: {IpAddress}, " +
                    "Request: {@Request}, Exception: {ExceptionType} - {ExceptionMessage}, StackTrace: {StackTrace}",
                    correlationId, ex.Message
                    
                );

                throw;
            }
        }


        //[HttpPut("FileAnalyticsUpdate")]
        //    public async Task<IActionResult> UpdateFileConfig(
        //int fileConfigId,
        //[FromForm] IFormFile? file,  // optional: allow new file upload or keep existing
        //[FromForm] string? description,
        //[FromForm] string? promptConfiguration,
        //[FromForm] int companyId,
        //[FromForm] int flgSave = 1)

        //public async Task<IActionResult> UpdateFileConfig([FromForm] FileAnalyticsUpdate model)
        //{
        //    // get userId from claims if available

        //    if (User?.Identity?.IsAuthenticated == true)
        //    {
        //        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        //        model.UpdatedBy = (userIdClaim != null && int.TryParse(userIdClaim.Value, out var uid)) ? uid : 0;
        //    }

        //    string? fileName = null;
        //    string? filePath = null;
        //    var updateReq = new List<FileAnalyticsUpdate>();

        //    try
        //    {
        //        if (model.File != null && model.File.Length > 0)
        //        {
        //            var webRoot = _env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
        //            var uploadDir = Path.Combine(webRoot, "Uploads/PDF/" + model.CompanyID);
        //            Directory.CreateDirectory(uploadDir);

        //            var safeName = Path.GetFileName(model.File.FileName);
        //            var fullPath = Path.Combine(uploadDir, safeName);

        //            await using (var stream = System.IO.File.Create(fullPath))
        //                await model.File.CopyToAsync(stream);

        //            fileName = safeName;
        //            filePath = fullPath.Replace('\\', '/');
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        return BadRequest(new { message = $"File upload failed: {ex.Message}" });
        //    }

        //    // build update model

        //    updateReq.Add(new FileAnalyticsUpdate
        //    {
        //        FileName = model.FileName,
        //        FilePath = model.FilePath,
        //        Description = model.Description,
        //        PromptConfiguration = model.PromptConfiguration,
        //        UploadedAt = DateTime.UtcNow,
        //        CompanyID = model.CompanyID,
        //        flgSave = model.flgSave,
        //        UpdatedBy = model.UpdatedBy
        //    });

        //    var spResult = await _repo.UpdateFileAnalyticsConfigAsync(updateReq);

        //    if (spResult.Status == true)
        //        return Ok(new { message = spResult.Message, fileConfigId = spResult.Data });

        //    if (spResult.Message?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
        //        return NotFound(new { message = spResult.Message });

        //    if (spResult.Message?.Contains("already exists", StringComparison.OrdinalIgnoreCase) == true)
        //        return Conflict(new { message = spResult.Message });

        //    return BadRequest(new { message = spResult.Message ?? "Update failed." });
        //}


        //[HttpPut("FileAnalyticsUpdate/{id}")]




        [HttpPut("FileAnalyticsUpdate")]
        public async Task<IActionResult> UpdateFileConfig(
    int id,
    [FromForm] List<IFormFile>? files,
    [FromForm] List<string>? descriptions,
    [FromForm] FileAnalyticsUpdate_req request)
        {
            var correlationId = HttpContext.TraceIdentifier;
            var username = User?.Identity?.Name ?? "anonymous";
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // Fill ModifiedBy from claims if available
            if (User?.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                request.UpdatedBy = (userIdClaim != null && int.TryParse(userIdClaim.Value, out var uid)) ? uid : 0;
            }
            else
            {
                request.UpdatedBy = 0;
            }

            var objUpdate = new List<FileAnalyticsUpdate>();
            var objSave = new List<FileAnalyticsCreate>();
            var spResult = new Response<int>();

            try
            {
                var webRoot = _env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
                var uploadDir = Path.Combine(webRoot, "Uploads/PDF/" + request.CompanyName);
                Directory.CreateDirectory(uploadDir);

                // Case 1: Update metadata only (no new files uploaded)
                if (files == null || files.Count == 0)
                {
                    objUpdate.Add(new FileAnalyticsUpdate
                    {
                        Description = descriptions?.FirstOrDefault(),
                        PromptConfiguration = request.PromptConfiguration,
                        CompanyID = request.CompanyID,
                        flgSave = 2, // update flag
                        UpdatedBy = request.UpdatedBy
                    });

                    spResult = await _repo.UpdateFileAnalyticsConfigAsync(objUpdate);
                }
                else
                {
                    // Case 2: Update + replace uploaded files
                    for (int i = 0; i < files.Count; i++)
                    {
                        var file = files[i];
                        if (file?.Length > 0)
                        {
                            var safeName = Path.GetFileName(file.FileName);
                            var uniqueName = $"{DateTime.UtcNow.Ticks}_{safeName}";
                            var fullPath = Path.Combine(uploadDir, safeName);

                            await using (var stream = System.IO.File.Create(fullPath))
                                await file.CopyToAsync(stream);

                            objSave.Add(new FileAnalyticsCreate
                            {
                                FileName = safeName,
                                FilePath = fullPath.Replace('\\', '/'),
                                Description = i < descriptions?.Count ? descriptions[i] : null,
                                PromptConfiguration = request.PromptConfiguration,
                                UploadedAt = DateTime.UtcNow,
                                CompanyID = request.CompanyID,
                                flgSave = 1,
                                CreatedBy = request.UpdatedBy
                            });
                        }
                    }

                    spResult = await _repo.SaveFileAnalyticsConfigAsync(objSave);
                }
            }
            catch (Exception ex)
            {
                // 🔹 ERROR LOGGING ONLY
                _logger.LogError(
                    ex,
                    "Error in FileAnalyticsUpdate API - CorrelationId: {CorrelationId}, " +
                    "User: {Username}, IP: {IpAddress}, " +
                    "Request: {@Request}, ExceptionType: {ExceptionType}, Message: {ExceptionMessage}, StackTrace: {StackTrace}",
                    correlationId,
                    ex.Message
                    
                );

                throw;
            }

            if (spResult.Status == true && spResult.Data > 0)
            {
                return Ok(new { message = spResult.Message, fileConfigId = id });
            }

            if (spResult.Message?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
                return NotFound(new { message = spResult.Message });

            return BadRequest(new { message = spResult.Message ?? "Update failed." });
        }



    

        // Delete
        [HttpDelete("DeleteFileConfig")]
        [SessionAuthorize]
        public async Task<IActionResult> DeleteFileConfig(DeleteFileConfig dtodelete)
        {
            var correlationId = HttpContext.TraceIdentifier;
            var username = User?.Identity?.Name ?? "anonymous";
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            try
            {
                // Identify DeletedBy user
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                    dtodelete.DeletedBy = userId;
                else
                    dtodelete.DeletedBy = 0;

                var response = await _repo.DeleteFileConfig(dtodelete).ConfigureAwait(false);
                return Ok(response);
            }
            catch (Exception ex)
            {
                // 🔹 ERROR LOGGING ONLY
                _logger.LogError(
                    ex,
                    "Error in DeleteFileConfig API - CorrelationId: {CorrelationId}, " +
                    "User: {Username}, IP: {IpAddress}, " +
                    "Request: {@Request}, ExceptionType: {ExceptionType}, Message: {ExceptionMessage}, StackTrace: {StackTrace}",
                    correlationId,
                    ex.Message
                );

                // Optional: safe return for API
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    message = "An error occurred while deleting the file configuration.",
                    correlationId,
                    error = ex.Message
                });
            }
        }


    }
}
