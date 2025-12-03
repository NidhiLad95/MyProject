using BAL.Interface;
using BOL;
using GenxAi_Solutions_V1.Models;
using GenxAi_Solutions_V1.Services;
using GenxAi_Solutions_V1.Services.Interfaces;
using GenxAi_Solutions_V1.Utils;
using Google.Protobuf;
using Microsoft.Agents.AI;
using Microsoft.Agents.Builder;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI;
using Org.BouncyCastle.Utilities.Collections;
using System.Data;
using System.Security.Claims;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;

namespace GenxAi_Solutions_V1.Api
{
    [ApiController]
    [Route("api/chat")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class ChatBotController
        (IChatBotBAL chat,
        ICompanyProfileBAL repo,
        ISqlConfigRepository configRepo,        
        ILogger<ChatBotController> log,        
        IVectorStoreFactory storeFac,       
        IEnumerable<ChatClientAgent> agents,
        IChatHistoryService history) : ControllerBase
    {

        private readonly IChatBotBAL _chat = chat;
        private readonly ICompanyProfileBAL _repo = repo;
        private readonly ISqlConfigRepository _configRepo = configRepo;
        private readonly ILogger<ChatBotController> _log = log;
        private readonly IVectorStoreFactory _storeFac = storeFac;
        private readonly ChatClientAgent _agent = agents.First(a =>  string.Equals(a.Name, "SqlQueryAgent", StringComparison.OrdinalIgnoreCase));
        private readonly ChatClientAgent _chartAgent = agents.First(a =>  string.Equals(a.Name, "ChartAgent", StringComparison.OrdinalIgnoreCase));
        private readonly ChatClientAgent _titleAgent = agents.First(a =>  string.Equals(a.Name, "TitleAgent", StringComparison.OrdinalIgnoreCase));
        private readonly ChatClientAgent _pdfAgent = agents.First(a => string.Equals(a.Name, "PdfAgent", StringComparison.OrdinalIgnoreCase));
        private readonly IChatHistoryService _history = history;



        [HttpPost("AskAgent")]
        [Consumes("application/x-www-form-urlencoded")]
        public async Task<IActionResult> AskAgent(
    [FromForm] string message,
    [FromForm] string? service,
    [FromForm] long? conversationId,
    CancellationToken ct)
        {
            var username = User?.Identity?.Name ?? "anonymous";
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var correlationId = HttpContext.TraceIdentifier;

            try
            {
                if (string.IsNullOrWhiteSpace(message))
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

                // Detect chart intent
                var wantsChart = System.Text.RegularExpressions.Regex.IsMatch(
                    message, @"\b(chart|graph(ical)|plot|visual(ize|isation|ization)?)\b",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                var sqlGreets = IsGreeting(message);

                long userMsgId = 0;
                long asstMsgId = 0;
                int tokensUsed = 0;
                string? replyTextForTitle = null;
                string? newTitle = null;

                // Create conversation lazily
                if (_chat != null && convId == 0)
                {
                    string titleSeed = message.Length > 40 ? message.Substring(0, 40) + "…" : message;
                    convId = (await _chat.StartConversationAsync(new StartConversation
                    {
                        UserId = userId,
                        Title = string.IsNullOrWhiteSpace(titleSeed) ? "New chat" : titleSeed
                    })).Data;

                    var sysPrompt = await GetPromptAsync(companyId, chosenService);
                    var rules = string.Equals(chosenService, "FileAnalytics", StringComparison.OrdinalIgnoreCase)
                        ? sysPrompt + "/n/n" + " You are a helpful assistant. Use prior messages as context." + "/n/n" + PromptService.Pdfrule
                        : sysPrompt + "/n/n" + PromptService.Rules;

                    await _chat.AppendMessageAsync(new AppendMessage
                    {
                        ConversationId = convId,
                        SenderType = "system",
                        SenderId = null,
                        Text = rules
                    });

                    InvalidateHistoryCache(convId);
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

                    InvalidateHistoryCache(convId);
                }

                //// ==================== FileAnalytics ====================
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

                    // Use Agent Framework for PDF query
                    var pdfResult = await QueryPdfWithAgent(message, dbPathPdf_File, convId,companyId, userId, userMsgId, ct);
                    return Ok(pdfResult);
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

                // Use Agent Framework for SQL query
                var sqlResult = await QuerySqlWithAgent(message, dbPathSql, connStr, databaseName, dbType,
                    convId, companyId, userId, wantsChart, sqlGreets, userMsgId, ct);

                return Ok(sqlResult);
            }
            catch (Exception ex)
            {
                _log.LogError(
                    ex,
                    "Error in AskAgent API - User: {Username}, Service: {Service}, Message: {Message}, CorrelationId: {CorrelationId}, Error: {ErrorMessage}",
                    username, service, message, correlationId, ex.Message);
                throw;
            }
        }

        private async Task<object> QuerySqlWithAgent5(string message, string dbPathSql, string connStr,

string databaseName, string dbType, long convId, int companyId, int userId,

bool wantsChart, bool sqlGreets, long userMsgId, CancellationToken ct)

        {

            int tokensUsed = 0;

            string? replyTextForTitle = null;

            string? newTitle = null;

            long asstMsgId = 0;

            // Create stores using Agent Framework

            var stores = _storeFac.Create_New(dbPathSql);

            // Check for forbidden SQL operations

            if (!sqlGreets && ContainsForbiddenWriteSql(message, out var badVerb))

            {

                await _chat.AppendMessageAsync(new AppendMessage

                {

                    ConversationId = convId,

                    SenderType = "assistant",

                    SenderId = null,

                    Text = $"{badVerb} operations are not allowed. I can only run safe SELECT queries."

                });

                InvalidateHistoryCache(convId);

                return new

                {

                    message = $"{badVerb} operations are not allowed. I can only run safe SELECT queries.",

                    data = new List<object>(),

                    tokensUsed,

                    conversationId = convId

                };

            }

            if (!sqlGreets)

            {

                // Use Agent Framework for vector search

                var hits = new List<VectorSearchResult<SchemaRecord>>();

                await foreach (var r in stores.Schemas.SearchAsync(message, top: 5))

                {

                    hits.Add(r);

                }

                if (hits.Count == 0)

                    return new

                    {

                        message = "No schema available in store. Seed schema first.",

                        data = new List<object>(),

                        tokensUsed,

                        conversationId = convId

                    };

                // Build schema context from hits

                var schemaContextBuilder = new System.Text.StringBuilder();

                schemaContextBuilder.AppendLine("Available database schema:");

                schemaContextBuilder.AppendLine("===========================");

                foreach (var hit in hits)

                {

                    schemaContextBuilder.AppendLine(hit.Record.SchemaText);

                    schemaContextBuilder.AppendLine();

                }

                // Get SQL rules from prompts

                var rules = await stores.Prompts.GetAsync("sql_rules");

                if (rules != null)

                {

                    schemaContextBuilder.AppendLine("### SQL RULES");

                    schemaContextBuilder.AppendLine(rules.Text);

                }

                string schemaContext = schemaContextBuilder.ToString();

                // Build messages for agent with proper context

                var historyMessages = await GetLimitedHistoryAsync_up(

                    conversationId: convId,

                    maxHistory: 20,

                    userMessage: message, // Pass original message, not the processed one

                    chosenService: "SQLAnalytics",

                    context: schemaContext, // Add schema context

                    ct: ct);

                // Call SQL agent

                var sqlAgentResponse = await _agent.RunAsync(historyMessages);

                var rawAnswer = sqlAgentResponse.Text?.Trim() ?? "(no text)";

                // Process SQL response

                if (rawAnswer.Contains("select", StringComparison.OrdinalIgnoreCase))

                {

                    var sql = SanitizeSql(rawAnswer);

                    if (_chat != null && userMsgId > 0 && !string.IsNullOrWhiteSpace(sql))

                        await _chat.AddMetadataAsync(userMsgId, "sql", sql);

                    tokensUsed += EstimateTokens(sql);

                    if (string.IsNullOrWhiteSpace(sql))

                    {

                        var msg = "Model did not return SQL.";

                        await HandleAssistantResponse(convId, msg, companyId, tokensUsed, "text", null);

                        return new { message = msg, data = new List<object>(), tokensUsed, conversationId = convId };

                    }

                    // Execute SQL and return results

                    return await ExecuteAndReturnResults(sql, dbType, connStr, databaseName, wantsChart,

                        convId, companyId, tokensUsed, stores, message, ct);

                }

                else

                {

                    // Handle text response

                    if (_chat != null && userMsgId > 0 && !string.IsNullOrWhiteSpace(rawAnswer))

                        await _chat.AddMetadataAsync(userMsgId, "Text", rawAnswer);

                    tokensUsed += EstimateTokens(rawAnswer);

                    await HandleAssistantResponse(convId, rawAnswer, companyId, tokensUsed, "text", null);

                    return new

                    {

                        message = rawAnswer,

                        data = new List<object>(),

                        tokensUsed,

                        conversationId = convId

                    };

                }

            }

            else

            {

                // Handle greeting case

                var greetingResponse = await HandleGreetingResponse(message, convId, companyId, stores, ct);

                return greetingResponse;

            }

        }


        private async Task<object> QuerySqlWithAgent6(
    string message,
    string dbPathSql,
    string connStr,
    string databaseName,
    string dbType,
    long convId,
    int companyId,
    int userId,
    bool wantsChart,
    bool sqlGreets,
    long userMsgId,
    CancellationToken ct)
        {
            int tokensUsed = 0;
            string? replyTextForTitle = null;
            string? newTitle = null;
            long asstMsgId = 0;

            // Create stores using Agent Framework
            var stores = _storeFac.Create_New(dbPathSql);

            // ✅ 1. Block write operations (unchanged core logic)
            if (!sqlGreets && ContainsForbiddenWriteSql(message, out var badVerb))
            {
                await HandleAssistantResponse(
                    convId,
                    $"{badVerb} operations are not allowed. I can only run safe SELECT queries.",
                    companyId,
                    tokensUsed,
                    "text",
                    null,
                    "SQLAnalytics");

                return new
                {
                    message = $"{badVerb} operations are not allowed. I can only run safe SELECT queries.",
                    data = new List<object>(),
                    tokensUsed,
                    conversationId = convId
                };
            }

            if (!sqlGreets)
            {
                // ✅ 2. Vector search in schema store (Agent Framework)
                var hits = new List<VectorSearchResult<SchemaRecord>>();

                await foreach (var r in stores.Schemas
                    .SearchAsync(message, top: 10)  // slightly higher top for better recall
                    .WithCancellation(ct))
                {
                    hits.Add(r);
                }

                // No hit at all => treat as "no schema available"
                if (hits.Count == 0)
                {
                    var msg = "No database schema found for this question. " +
                              "Please seed schema for this company first.";
                    return new
                    {
                        message = msg,
                        data = new List<object>(),
                        tokensUsed,
                        conversationId = convId
                    };
                }

                // ✅ 3. Filter hits: score + table/column relevance
                var filteredHits = FilterSchemaHits(message, hits);

                // If nothing survives filtering, treat as "no matching table / columns"
                if (filteredHits.Count == 0)
                {
                    var msg = "I could not find any relevant tables or columns in the seeded schema " +
                              "for this question. Please check your table/column name or re-run schema seeding.";
                    return new
                    {
                        message = msg,
                        data = new List<object>(),
                        tokensUsed,
                        conversationId = convId
                    };
                }

                // Optional logging (does not change core flow)
                try
                {
                    var bestScore = hits.Max(h => h.Score);
                    _log.LogInformation(
                        "SQL schema search: totalHits={Total}, filteredHits={Filtered}, bestScore={BestScore}, ConversationId={ConversationId}",
                        hits.Count,
                        filteredHits.Count,
                        bestScore,
                        convId);
                }
                catch { /* logging should never break flow */ }

                // ✅ 4. Build focused schema context (matching tables + filtered columns)
                string schemaContext = BuildSqlSchemaContext(message, filteredHits);

                // ✅ 5. Build messages for agent WITH proper context (unchanged pattern)
                var historyMessages = await GetLimitedHistoryAsync_up(
                    conversationId: convId,
                    maxHistory: 20,
                    userMessage: message, // original message
                    chosenService: "SQLAnalytics",
                    context: schemaContext,
                    ct: ct);

                // ✅ 6. Call SQL agent (same as your core logic)
                var sqlAgentResponse = await _agent.RunAsync(historyMessages);
                var rawAnswer = sqlAgentResponse.Text?.Trim() ?? "(no text)";

                // ✅ 7. Process SQL response (unchanged core flow)
                if (rawAnswer.Contains("select", StringComparison.OrdinalIgnoreCase))
                {
                    var sql = SanitizeSql(rawAnswer);

                    if (_chat != null && userMsgId > 0 && !string.IsNullOrWhiteSpace(sql))
                        await _chat.AddMetadataAsync(userMsgId, "sql", sql);

                    tokensUsed += EstimateTokens(sql);

                    if (string.IsNullOrWhiteSpace(sql))
                    {
                        var msg = "Model did not return SQL.";
                        await HandleAssistantResponse(convId, msg, companyId, tokensUsed, "text", null);
                        return new { message = msg, data = new List<object>(), tokensUsed, conversationId = convId };
                    }

                    // Execute SQL and return results (unchanged)
                    return await ExecuteAndReturnResults(
                        sql,
                        dbType,
                        connStr,
                        databaseName,
                        wantsChart,
                        convId,
                        companyId,
                        tokensUsed,
                        stores,
                        message,
                        ct);
                }
                else
                {
                    // Text / explanation response (unchanged)
                    if (_chat != null && userMsgId > 0 && !string.IsNullOrWhiteSpace(rawAnswer))
                        await _chat.AddMetadataAsync(userMsgId, "Text", rawAnswer);

                    tokensUsed += EstimateTokens(rawAnswer);
                    await HandleAssistantResponse(convId, rawAnswer, companyId, tokensUsed, "text", null);

                    return new
                    {
                        message = rawAnswer,
                        data = new List<object>(),
                        tokensUsed,
                        conversationId = convId
                    };
                }
            }
            else
            {
                // ✅ 8. Greeting case (unchanged)
                var greetingResponse = await HandleGreetingResponse(message, convId, companyId, stores, ct);
                return greetingResponse;
            }
        }

        private async Task<object> QuerySqlWithAgent7(
    string message,
    string dbPathSql,
    string connStr,
    string databaseName,
    string dbType,
    long convId,
    int companyId,
    int userId,
    bool wantsChart,
    bool sqlGreets,
    long userMsgId,
    CancellationToken ct)
        {
            int tokensUsed = 0;
            string? replyTextForTitle = null;
            string? newTitle = null;
            long asstMsgId = 0;

            // Create stores using Agent Framework
            var stores = _storeFac.Create_New(dbPathSql);

            // 1. Block write operations (unchanged core logic)
            if (!sqlGreets && ContainsForbiddenWriteSql(message, out var badVerb))
            {
                await HandleAssistantResponse(
                    convId,
                    $"{badVerb} operations are not allowed. I can only run safe SELECT queries.",
                    companyId,
                    tokensUsed,
                    "text",
                    null,
                    "SQLAnalytics");

                return new
                {
                    message = $"{badVerb} operations are not allowed. I can only run safe SELECT queries.",
                    data = new List<object>(),
                    tokensUsed,
                    conversationId = convId
                };
            }

            // 2. Main SQL question flow (non-greeting)
            if (!sqlGreets)
            {
                // 2.1 Run schema RAG via helper
                var ragResult = await SqlSchemaRagTool.BuildSchemaContextAsync(message, stores, ct);

                // No schema at all (vector DB empty)
                if (!ragResult.HasAnySchema)
                {
                    var msg = "No database schema found for this question. " +
                              "Please seed schema for this company first.";
                    return new
                    {
                        message = msg,
                        data = new List<object>(),
                        tokensUsed,
                        conversationId = convId
                    };
                }

                // Schema exists but nothing relevant to this question
                if (!ragResult.HasRelevantSchema)
                {
                    var msg = "I could not find any relevant tables or columns in the seeded schema " +
                              "for this question. Please check your table/column name or re-run schema seeding.";
                    return new
                    {
                        message = msg,
                        data = new List<object>(),
                        tokensUsed,
                        conversationId = convId
                    };
                }

                // Optional logging
                try
                {
                    var bestScore = ragResult.FilteredHits.Max(h => h.Score ?? 0d);
                    _log.LogInformation(
                        "SQL schema search (RAG helper): totalHits={Total}, filteredHits={Filtered}, bestScore={BestScore}, ConversationId={ConversationId}",
                        ragResult.TotalHits,
                        ragResult.FilteredHits.Count,
                        bestScore,
                        convId);
                }
                catch
                {
                    // logging must never break flow
                }

                // 2.2 Use schema context from helper
                var schemaContext = ragResult.SchemaContext;

                // 2.3 Build messages for agent WITH context
                var historyMessages = await GetLimitedHistoryAsync_up(
                    conversationId: convId,
                    maxHistory: 20,
                    userMessage: message,
                    chosenService: "SQLAnalytics",
                    context: schemaContext,
                    ct: ct);

                // 2.4 Call SQL agent
                var sqlAgentResponse = await _agent.RunAsync(historyMessages);
                var rawAnswer = sqlAgentResponse.Text?.Trim() ?? "(no text)";

                // 2.5 Process SQL response
                if (rawAnswer.Contains("select", StringComparison.OrdinalIgnoreCase))
                {
                    var sql = SanitizeSql(rawAnswer);

                    if (_chat != null && userMsgId > 0 && !string.IsNullOrWhiteSpace(sql))
                        await _chat.AddMetadataAsync(userMsgId, "sql", sql);

                    tokensUsed += EstimateTokens(sql);

                    if (string.IsNullOrWhiteSpace(sql))
                    {
                        var msg = "Model did not return SQL.";
                        await HandleAssistantResponse(convId, msg, companyId, tokensUsed, "text", null);

                        return new
                        {
                            message = msg,
                            data = new List<object>(),
                            tokensUsed,
                            conversationId = convId
                        };
                    }

                    // 2.6 Execute SQL and return results (unchanged)
                    return await ExecuteAndReturnResults(
                        sql,
                        dbType,
                        connStr,
                        databaseName,
                        wantsChart,
                        convId,
                        companyId,
                        tokensUsed,
                        stores,
                        message,
                        ct);
                }
                else
                {
                    // Text / explanation response
                    if (_chat != null && userMsgId > 0 && !string.IsNullOrWhiteSpace(rawAnswer))
                        await _chat.AddMetadataAsync(userMsgId, "Text", rawAnswer);

                    tokensUsed += EstimateTokens(rawAnswer);

                    await HandleAssistantResponse(
                        convId,
                        rawAnswer,
                        companyId,
                        tokensUsed,
                        "text",
                        null);

                    return new
                    {
                        message = rawAnswer,
                        data = new List<object>(),
                        tokensUsed,
                        conversationId = convId
                    };
                }
            }
            else
            {
                // 3. Greeting path (unchanged)
                var greetingResponse = await HandleGreetingResponse(message, convId, companyId, stores, ct);
                return greetingResponse;
            }
        }

        private async Task<object> QuerySqlWithAgent(
    string message,
    string dbPathSql,
    string connStr,
    string databaseName,
    string dbType,
    long convId,
    int companyId,
    int userId,
    bool wantsChart,
    bool sqlGreets,
    long userMsgId,
    CancellationToken ct)
        {
            int tokensUsed = 0;
            string? replyTextForTitle = null;
            string? newTitle = null;
            long asstMsgId = 0;

            // Start timing for performance monitoring
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var timingLogs = new List<(string, long)>();

            try
            {
                // Create stores using Agent Framework - track timing
                var storeCreationStopwatch = System.Diagnostics.Stopwatch.StartNew();
                var stores = _storeFac.Create_New(dbPathSql);
                storeCreationStopwatch.Stop();
                timingLogs.Add(("Store Creation", storeCreationStopwatch.ElapsedMilliseconds));

                // 1. Block write operations (unchanged core logic)
                if (!sqlGreets && ContainsForbiddenWriteSql(message, out var badVerb))
                {
                    await HandleAssistantResponse(
                        convId,
                        $"{badVerb} operations are not allowed. I can only run safe SELECT queries.",
                        companyId,
                        tokensUsed,
                        "text",
                        null,
                        "SQLAnalytics");

                    return new
                    {
                        message = $"{badVerb} operations are not allowed. I can only run safe SELECT queries.",
                        data = new List<object>(),
                        tokensUsed,
                        conversationId = convId
                    };
                }

                // 2. Main SQL question flow (non-greeting)
                if (!sqlGreets)
                {
                    // 2.1 Run schema RAG via helper - track timing
                    var ragStopwatch = System.Diagnostics.Stopwatch.StartNew();
                    var ragResult = await SqlSchemaRagTool.BuildSchemaContextAsync(message, stores, ct);
                    ragStopwatch.Stop();
                    timingLogs.Add(("RAG Search", ragStopwatch.ElapsedMilliseconds));

                    // No schema at all (vector DB empty)
                    if (!ragResult.HasAnySchema)
                    {
                        var msg = "No database schema found for this question. " +
                                  "Please seed schema for this company first.";
                        return new
                        {
                            message = msg,
                            data = new List<object>(),
                            tokensUsed,
                            conversationId = convId
                        };
                    }

                    // Schema exists but nothing relevant to this question
                    if (!ragResult.HasRelevantSchema)
                    {
                        var msg = "I could not find any relevant tables or columns in the seeded schema " +
                                  "for this question. Please check your table/column name or re-run schema seeding.";
                        return new
                        {
                            message = msg,
                            data = new List<object>(),
                            tokensUsed,
                            conversationId = convId
                        };
                    }

                    // Optional logging with performance metrics
                    try
                    {
                        var bestScore = ragResult.FilteredHits.Max(h => h.Score ?? 0d);
                        _log.LogInformation(
                            "SQL schema search (RAG helper): totalHits={Total}, filteredHits={Filtered}, bestScore={BestScore}, ConversationId={ConversationId}, SearchTime={SearchTime}ms",
                            ragResult.TotalHits,
                            ragResult.FilteredHits.Count,
                            bestScore,
                            convId,
                            ragStopwatch.ElapsedMilliseconds);
                    }
                    catch
                    {
                        // logging must never break flow
                    }

                    // 2.2 Use schema context from helper
                    var schemaContext = ragResult.SchemaContext;

                    // 2.3 Build messages for agent WITH context - run in parallel with other tasks
                    var historyMessagesTask = GetLimitedHistoryAsync_up(
                        conversationId: convId,
                        maxHistory: 20,
                        userMessage: message,
                        chosenService: "SQLAnalytics",
                        context: schemaContext,
                        ct: ct);

                    // 2.4 Call SQL agent - await history first, then call agent
                    var historyMessages = await historyMessagesTask;

                    var agentCallStopwatch = System.Diagnostics.Stopwatch.StartNew();
                    var sqlAgentResponse = await _agent.RunAsync(historyMessages);
                    agentCallStopwatch.Stop();
                    timingLogs.Add(("Agent Call", agentCallStopwatch.ElapsedMilliseconds));

                    var rawAnswer = sqlAgentResponse.Text?.Trim() ?? "(no text)";

                    // 2.5 Process SQL response
                    if (rawAnswer.Contains("select", StringComparison.OrdinalIgnoreCase))
                    {
                        var sql = SanitizeSql(rawAnswer);

                        // Start metadata updates in background to not block response
                        var metadataTask = Task.Run(async () =>
                        {
                            if (_chat != null && userMsgId > 0 && !string.IsNullOrWhiteSpace(sql))
                                await _chat.AddMetadataAsync(userMsgId, "sql", sql);
                        }, ct);

                        tokensUsed += EstimateTokens(sql);

                        if (string.IsNullOrWhiteSpace(sql))
                        {
                            var msg = "Model did not return SQL.";

                            // Wait for metadata task to complete
                            await metadataTask;

                            await HandleAssistantResponse(convId, msg, companyId, tokensUsed, "text", null);

                            return new
                            {
                                message = msg,
                                data = new List<object>(),
                                tokensUsed,
                                conversationId = convId
                            };
                        }

                        // 2.6 Execute SQL and return results - don't wait for metadata task
                        var executeTask = ExecuteAndReturnResults(
                            sql,
                            dbType,
                            connStr,
                            databaseName,
                            wantsChart,
                            convId,
                            companyId,
                            tokensUsed,
                            stores,
                            message,
                            ct);

                        // Wait for both execution and metadata tasks
                        var result = await executeTask;

                        // Ensure metadata task completes but don't wait if it's taking too long
                        if (!metadataTask.IsCompleted)
                        {
                            await Task.WhenAny(metadataTask, Task.Delay(100, ct));
                        }

                        // Log performance metrics
                        stopwatch.Stop();
                        timingLogs.Add(("Total Execution", stopwatch.ElapsedMilliseconds));

                        if (stopwatch.ElapsedMilliseconds > 1000) // Log slow requests
                        {
                            var timingSummary = string.Join(", ", timingLogs.Select(t => $"{t.Item1}: {t.Item2}ms"));
                            _log.LogInformation(
                                "QuerySqlWithAgent performance - ConversationId={ConversationId}, TotalTime={TotalTime}ms, Breakdown: {Timing}",
                                convId, stopwatch.ElapsedMilliseconds, timingSummary);
                        }

                        return result;
                    }
                    else
                    {
                        // Text / explanation response
                        if (_chat != null && userMsgId > 0 && !string.IsNullOrWhiteSpace(rawAnswer))
                            await _chat.AddMetadataAsync(userMsgId, "Text", rawAnswer);

                        tokensUsed += EstimateTokens(rawAnswer);

                        await HandleAssistantResponse(
                            convId,
                            rawAnswer,
                            companyId,
                            tokensUsed,
                            "text",
                            null);

                        // Log performance metrics
                        stopwatch.Stop();
                        timingLogs.Add(("Total Execution", stopwatch.ElapsedMilliseconds));

                        if (stopwatch.ElapsedMilliseconds > 1000)
                        {
                            var timingSummary = string.Join(", ", timingLogs.Select(t => $"{t.Item1}: {t.Item2}ms"));
                            _log.LogInformation(
                                "QuerySqlWithAgent text response - ConversationId={ConversationId}, TotalTime={TotalTime}ms",
                                convId, stopwatch.ElapsedMilliseconds);
                        }

                        return new
                        {
                            message = rawAnswer,
                            data = new List<object>(),
                            tokensUsed,
                            conversationId = convId
                        };
                    }
                }
                else
                {
                    // 3. Greeting path (unchanged)
                    var greetingResponse = await HandleGreetingResponse(message, convId, companyId, stores, ct);

                    // Log performance for greetings too
                    stopwatch.Stop();
                    if (stopwatch.ElapsedMilliseconds > 1000)
                    {
                        _log.LogInformation(
                            "QuerySqlWithAgent greeting - ConversationId={ConversationId}, TotalTime={TotalTime}ms",
                            convId, stopwatch.ElapsedMilliseconds);
                    }

                    return greetingResponse;
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _log.LogError(ex,
                    "QuerySqlWithAgent failed after {ElapsedMs}ms - ConversationId={ConversationId}, Message={Message}",
                    stopwatch.ElapsedMilliseconds, convId, message);
                throw;
            }
        }

        /// <summary>
        /// Filters raw schema hits using score + (optionally) TableName/ColumnName.
        /// Returns only strongly relevant hits.
        /// </summary>
        private static IReadOnlyList<VectorSearchResult<SchemaRecord>> FilterSchemaHits(
    string userMessage,
    List<VectorSearchResult<SchemaRecord>> hits)
        {
            if (hits == null || hits.Count == 0)
                return Array.Empty<VectorSearchResult<SchemaRecord>>();

            // Sort by descending vector score (Score is double?)
            var ordered = hits
                .OrderByDescending(h => h.Score ?? 0d)
                .ToList();

            double bestScore = ordered[0].Score ?? 0d;

            // If score is not populated, just return top few
            if (bestScore <= 0d)
                return ordered.Take(10).ToList();

            // Basic score thresholds (you can tweak)
            const double minAbsoluteScore = 0.30;  // ignore very weak matches
            const double relativeCut = 0.60;       // keep hits close to best one

            double threshold = System.Math.Max(minAbsoluteScore, bestScore * relativeCut);

            var aboveThreshold = ordered
                .Where(h => (h.Score ?? 0d) >= threshold)
                .ToList();

            if (aboveThreshold.Count == 0)
                return Array.Empty<VectorSearchResult<SchemaRecord>>();

            // Try to use TableName / ColumnName properties if SchemaRecord exposes them
            var schemaType = typeof(SchemaRecord);
            var tableNameProp = schemaType.GetProperty("TableName");
            var columnNameProp = schemaType.GetProperty("ColumnName");

            // If SchemaRecord has no table/column metadata, just return top hits
            if (tableNameProp == null && columnNameProp == null)
                return aboveThreshold.Take(10).ToList();

            var tokens = ExtractCandidateNames(userMessage);

            var grouped = aboveThreshold
                .GroupBy(h =>
                {
                    var tableName = tableNameProp?.GetValue(h.Record) as string;
                    return string.IsNullOrWhiteSpace(tableName) ? "__UNKNOWN__" : tableName!;
                });

            var tableScores = new List<(string TableName, double Score, bool ExplicitMatch, List<VectorSearchResult<SchemaRecord>> Rows)>();

            foreach (var g in grouped)
            {
                var rows = g.ToList();

                // maxScore as non-nullable double
                double maxScore = rows.Max(h => h.Score ?? 0d);

                var tableName = g.Key;
                bool explicitMatch = false;

                // Check if table name is explicitly mentioned in the question
                if (tableNameProp != null && tableName != "__UNKNOWN__")
                {
                    if (tokens.Contains(tableName.ToLowerInvariant()))
                        explicitMatch = true;
                }

                // Check if any column is explicitly mentioned
                if (!explicitMatch && columnNameProp != null)
                {
                    foreach (var r in rows)
                    {
                        var colName = columnNameProp.GetValue(r.Record) as string;
                        if (!string.IsNullOrWhiteSpace(colName) &&
                            tokens.Contains(colName.ToLowerInvariant()))
                        {
                            explicitMatch = true;
                            break;
                        }
                    }
                }

                tableScores.Add((tableName, maxScore, explicitMatch, rows));
            }

            // If there are explicit table/column mentions, keep only those tables
            var candidates = tableScores;
            if (tableScores.Any(t => t.ExplicitMatch))
            {
                candidates = tableScores
                    .Where(t => t.ExplicitMatch)
                    .ToList();
            }

            // Pick top 5 tables, keep up to 10 rows per table
            var topTables = candidates
                .OrderByDescending(t => t.Score)
                .Take(5)
                .ToList();

            var finalHits = new List<VectorSearchResult<SchemaRecord>>();
            foreach (var t in topTables)
            {
                finalHits.AddRange(t.Rows.Take(10));
            }

            return finalHits;
        }



        /// <summary>
        /// Builds a compact schema context string for the agent:
        /// - Groups by table
        /// - Prefers columns that appear in the user question
        /// - Falls back to full SchemaText if needed
        /// </summary>
        private static string BuildSqlSchemaContext(
            string userMessage,
            IReadOnlyList<VectorSearchResult<SchemaRecord>> hits)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Available database schema (most relevant tables for this question):");
            sb.AppendLine("==================================================================");

            if (hits == null || hits.Count == 0)
                return sb.ToString();

            var schemaType = typeof(SchemaRecord);
            var tableNameProp = schemaType.GetProperty("TableName");
            var columnNameProp = schemaType.GetProperty("ColumnName");
            var dataTypeProp = schemaType.GetProperty("DataType"); // optional, will be null if not present

            var tokens = ExtractCandidateNames(userMessage);

            if (tableNameProp != null)
            {
                var grouped = hits
                    .GroupBy(h =>
                    {
                        var name = tableNameProp.GetValue(h.Record) as string;
                        return string.IsNullOrWhiteSpace(name) ? "__UNKNOWN__" : name;
                    })
                    .OrderByDescending(g => g.Max(h => h.Score));

                foreach (var g in grouped)
                {
                    var tableName = g.Key;

                    sb.AppendLine();
                    if (tableName != "__UNKNOWN__")
                        sb.AppendLine($"TABLE: {tableName}");

                    if (columnNameProp != null)
                    {
                        // Build unique column list for this table
                        var columns = g
                            .Select(h => new
                            {
                                Name = columnNameProp.GetValue(h.Record) as string,
                                DataType = dataTypeProp?.GetValue(h.Record) as string
                            })
                            .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                            .GroupBy(c => c.Name!, StringComparer.OrdinalIgnoreCase)
                            .Select(grp => new
                            {
                                Name = grp.Key,
                                DataType = grp.Select(x => x.DataType)
                                              .FirstOrDefault(d => !string.IsNullOrWhiteSpace(d)) ?? string.Empty
                            })
                            .ToList();

                        var mentioned = columns
                            .Where(c => tokens.Contains(c.Name.ToLowerInvariant()))
                            .ToList();

                        var colsToWrite = mentioned.Count > 0 ? mentioned : columns;

                        sb.AppendLine("Columns:");
                        foreach (var col in colsToWrite.Take(25))
                        {
                            if (string.IsNullOrWhiteSpace(col.DataType))
                                sb.AppendLine($" - {col.Name}");
                            else
                                sb.AppendLine($" - {col.Name} ({col.DataType})");
                        }
                    }
                    else
                    {
                        // No column-level metadata => fall back to original SchemaText
                        foreach (var hit in g)
                        {
                            sb.AppendLine(hit.Record.SchemaText);
                            sb.AppendLine();
                        }
                    }
                }
            }
            else
            {
                // No TableName property => original behaviour, just sorted by score
                foreach (var hit in hits.OrderByDescending(h => h.Score))
                {
                    sb.AppendLine(hit.Record.SchemaText);
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        /// <summary>
/// Extracts simple tokens (table/column candidates) from the user message.
/// Used to check explicit mentions of table / column names.
/// </summary>
private static HashSet<string> ExtractCandidateNames(string text)
{
    var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    if (string.IsNullOrWhiteSpace(text))
        return set;

    var cleaned = System.Text.RegularExpressions.Regex.Replace(
        text,
        @"[^A-Za-z0-9_]+",
        " ");

    foreach (var token in cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries))
    {
        var t = token.Trim();
        if (t.Length < 2) continue;
        set.Add(t.ToLowerInvariant());
    }

    return set;
}


        

        private async Task<object> QueryPdfWithAgent1(
    string message,
    string dbPathPdf,
    long convId,
    int companyId,
    int userId,
    long userMsgId,
    CancellationToken ct)
        {
            int tokensUsed = 0;
            string? replyTextForTitle = null;
            string? newTitle = null;
            long asstMsgId = 0;

            try
            {
                // 1) Basic validation – missing PDF DB claim
                if (string.IsNullOrWhiteSpace(dbPathPdf))
                {
                    var msg = "PDF database is not configured for this company. Please complete File Analytics onboarding and upload PDFs.";
                    _log.LogWarning("QueryPdfWithAgent called with empty dbPathPdf. ConversationId={ConversationId}", convId);

                    await HandleAssistantResponse(convId, msg, companyId, tokensUsed, "text", null, "FileAnalytics");

                    return new
                    {
                        message = msg,
                        data = new List<object>(),
                        tokensUsed,
                        conversationId = convId
                    };
                }

                _log.LogInformation(
                    "QueryPdfWithAgent start. dbPathPdf={DbPath}, ConversationId={ConversationId}",
                    dbPathPdf, convId);

                // 2) Create PDF stores using Agent Framework
                var pdfStores = _storeFac.PdfCreate_New(dbPathPdf);
                _log.LogInformation("PdfCreate_New completed for {DbPath}", dbPathPdf);

                // 3) Search chapters using Agent Framework vector search
                var pdfHits = new List<VectorSearchResult<PdfChapterRecord>>();

                await foreach (var r in pdfStores.Chapters.SearchAsync(message, top: 10)
                                   .WithCancellation(ct))
                {
                    pdfHits.Add(r);
                }

                _log.LogInformation(
                    "PDF vector search returned {HitCount} hits for ConversationId={ConversationId}",
                    pdfHits.Count, convId);

                if (pdfHits.Count == 0)
                {
                    var msg = "No relevant document context found. Please upload/seed PDFs first.";
                    await HandleAssistantResponse(convId, msg, companyId, tokensUsed, "text", null, "FileAnalytics");

                    return new
                    {
                        message = msg,
                        data = new List<object>(),
                        tokensUsed,
                        conversationId = convId
                    };
                }

                //// 4) Build PDF context from hits
                //var pdfContextBuilder = new System.Text.StringBuilder();
                //pdfContextBuilder.AppendLine("Available PDF document context:");
                //pdfContextBuilder.AppendLine("================================");

                //foreach (var hit in pdfHits)
                //{
                //    pdfContextBuilder.AppendLine($"Book: {hit.Record.BookName}");
                //    pdfContextBuilder.AppendLine($"Content: {hit.Record.Text}");
                //    pdfContextBuilder.AppendLine($"Relevance Score: {hit.Score:F4}");
                //    pdfContextBuilder.AppendLine("---");
                //}

                //var pdfContext = pdfContextBuilder.ToString();

                // 4) Build compact PDF context from hits
                var pdfContextBuilder = new System.Text.StringBuilder();
                pdfContextBuilder.AppendLine("Available PDF document context:");
                pdfContextBuilder.AppendLine("================================");

                // Order by score and take top few
                var ordered = pdfHits
                    .OrderByDescending(h => h.Score ?? 0d)
                    .Take(6)
                    .ToList();

                // Group by book
                foreach (var group in ordered.GroupBy(h => h.Record.BookName ?? "Unknown"))
                {
                    pdfContextBuilder.AppendLine($"Book: {group.Key}");

                    foreach (var hit in group.Take(2)) // max 2 sections per book
                    {
                        var text = hit.Record.Text ?? string.Empty;

                        if (text.Length > 500)
                            text = text.Substring(0, 500) + "...";

                        pdfContextBuilder.AppendLine($"Content: {text}");
                        pdfContextBuilder.AppendLine();
                    }

                    pdfContextBuilder.AppendLine("---");
                }

                var pdfContext = pdfContextBuilder.ToString();

                // 5) Build messages for PDF agent WITH context
                var historyMessages = await GetLimitedHistoryAsync_up(
                    conversationId: convId,
                    maxHistory: 20,
                    userMessage: message,
                    chosenService: "FileAnalytics",
                    context: pdfContext,    // <== IMPORTANT
                    ct: ct);

                _log.LogInformation(
                    "History built for PDF agent. MessageCount={Count}, ConversationId={ConversationId}",
                    historyMessages.Count, convId);

                // 6) Call PDF agent
                var pdfAgentResponse = await _pdfAgent.RunAsync(historyMessages, cancellationToken: ct);
                var answer = pdfAgentResponse.Text?.Trim() ?? "(no answer)";

                _log.LogInformation(
                    "PdfAgent responded. AnswerLength={Length}, ConversationId={ConversationId}",
                    answer.Length, convId);

                tokensUsed += EstimateTokens(answer);

                // 7) Persist assistant message + metadata
                if (_chat != null && convId > 0)
                {
                    var asstResp = await _chat.AppendMessageAsync(new AppendMessage
                    {
                        ConversationId = convId,
                        SenderType = "assistant",
                        SenderId = null,
                        Text = answer
                    });
                    asstMsgId = asstResp.Data;

                    var metadata = new Dictionary<string, string>
                    {
                        ["mode"] = "text",
                        ["service"] = "FileAnalytics",
                        ["tokensUsed"] = tokensUsed.ToString(),
                        ["pdfSources"] = pdfHits.Count.ToString(),
                        ["pdfDocuments"] = string.Join(", ", pdfHits.Select(h => h.Record.BookName).Distinct())
                    };

                    await _chat.AddMetadataBulkAsync(asstMsgId, metadata);

                    await _chat.IncrementConversationTokensAsync(new IncrementConversationTokens
                    {
                        ConversationId = convId,
                        Tokens = tokensUsed
                    });

                    replyTextForTitle = answer;
                    if (!string.IsNullOrWhiteSpace(replyTextForTitle))
                    {
                        newTitle = await TryUpdateTitleAsync(convId, message, replyTextForTitle, null, ct);
                    }

                    InvalidateHistoryCache(convId);
                }

                return new
                {
                    message = answer,
                    data = new List<object>(),
                    tokensUsed,
                    conversationId = convId,
                    newTitle
                };
            }
            catch (Exception ex)
            {
                _log.LogError(
                    ex,
                    "Error in QueryPdfWithAgent. Message={Message}, dbPathPdf={DbPath}, ConversationId={ConversationId}",
                    message, dbPathPdf, convId);

                var errorMsg = "Error processing PDF query. Please try again.";

                await HandleAssistantResponse(convId, errorMsg, companyId, tokensUsed, "text", null, "FileAnalytics");

                return new
                {
                    message = errorMsg,
                    data = new List<object>(),
                    tokensUsed,
                    conversationId = convId
                };
            }
        }

        private async Task<object> QueryPdfWithAgent(
        string message,
        string dbPathPdf,
        long convId,
        int companyId,
        int userId,
        long userMsgId,
        CancellationToken ct)
        {
            int tokensUsed = 0;
            string? replyTextForTitle = null;
            string? newTitle = null;
            long asstMsgId = 0;

            try
            {
                // 1) Basic validation – missing PDF DB claim
                if (string.IsNullOrWhiteSpace(dbPathPdf))
                {
                    var msg = "PDF database is not configured for this company. Please complete File Analytics onboarding and upload PDFs.";
                    _log.LogWarning("QueryPdfWithAgent called with empty dbPathPdf. ConversationId={ConversationId}", convId);

                    await HandleAssistantResponse(convId, msg, companyId, tokensUsed, "text", null, "FileAnalytics");

                    return new
                    {
                        message = msg,
                        data = new List<object>(),
                        tokensUsed,
                        conversationId = convId
                    };
                }

                _log.LogInformation(
                    "QueryPdfWithAgent start. dbPathPdf={DbPath}, ConversationId={ConversationId}",
                    dbPathPdf, convId);

                // 2) Create PDF stores using Agent Framework
                var pdfStores = _storeFac.PdfCreate_New(dbPathPdf);
                _log.LogInformation("PdfCreate_New completed for {DbPath}", dbPathPdf);

                // 🔻🔻🔻 REPLACE THE OLD SEARCH + CONTEXT BLOCK WITH THIS 🔻🔻🔻

                // 3) Use helper to search chapters + build compact context
                var pdfRagResult = await PdfRagTool.BuildPdfContextAsync(message, pdfStores, ct);

                _log.LogInformation(
                    "PDF vector search returned {HitCount} hits for ConversationId={ConversationId}",
                    pdfRagResult.Hits.Count, convId);

                if (!pdfRagResult.HasAnyContext)
                {
                    var msg = "No relevant document context found. Please upload/seed PDFs first.";
                    await HandleAssistantResponse(convId, msg, companyId, tokensUsed, "text", null, "FileAnalytics");

                    return new
                    {
                        message = msg,
                        data = new List<object>(),
                        tokensUsed,
                        conversationId = convId
                    };
                }

                // We still need hits for metadata & context for agent
                var pdfHits = pdfRagResult.Hits;
                var pdfContext = pdfRagResult.PdfContext;

                // 5) Build messages for PDF agent WITH context
                var historyMessages = await GetLimitedHistoryAsync_up(
                    conversationId: convId,
                    maxHistory: 20,
                    userMessage: message,
                    chosenService: "FileAnalytics",
                    context: pdfContext,
                    ct: ct);

                _log.LogInformation(
                    "History built for PDF agent. MessageCount={Count}, ConversationId={ConversationId}",
                    historyMessages.Count, convId);

                // 6) Call PDF agent
                var pdfAgentResponse = await _pdfAgent.RunAsync(historyMessages, cancellationToken: ct);
                var answer = pdfAgentResponse.Text?.Trim() ?? "(no answer)";

                _log.LogInformation(
                    "PdfAgent responded. AnswerLength={Length}, ConversationId={ConversationId}",
                    answer.Length, convId);

                tokensUsed += EstimateTokens(answer);

                // 7) Persist assistant message + metadata
                if (_chat != null && convId > 0)
                {
                    var asstResp = await _chat.AppendMessageAsync(new AppendMessage
                    {
                        ConversationId = convId,
                        SenderType = "assistant",
                        SenderId = null,
                        Text = answer
                    });
                    asstMsgId = asstResp.Data;

                    var metadata = new Dictionary<string, string>
                    {
                        ["mode"] = "text",
                        ["service"] = "FileAnalytics",
                        ["tokensUsed"] = tokensUsed.ToString(),
                        ["pdfSources"] = pdfHits.Count.ToString(),
                        ["pdfDocuments"] = string.Join(", ", pdfHits.Select(h => h.Record.BookName).Distinct())
                    };

                    await _chat.AddMetadataBulkAsync(asstMsgId, metadata);

                    await _chat.IncrementConversationTokensAsync(new IncrementConversationTokens
                    {
                        ConversationId = convId,
                        Tokens = tokensUsed
                    });

                    replyTextForTitle = answer;
                    if (!string.IsNullOrWhiteSpace(replyTextForTitle))
                    {
                        newTitle = await TryUpdateTitleAsync(convId, message, replyTextForTitle, null, ct);
                    }

                    InvalidateHistoryCache(convId);
                }

                return new
                {
                    message = answer,
                    data = new List<object>(),
                    tokensUsed,
                    conversationId = convId,
                    newTitle
                };
            }
            catch (Exception ex)
            {
                _log.LogError(
                    ex,
                    "Error in QueryPdfWithAgent. Message={Message}, dbPathPdf={DbPath}, ConversationId={ConversationId}",
                    message, dbPathPdf, convId);

                var errorMsg = "Error processing PDF query. Please try again.";

                await HandleAssistantResponse(convId, errorMsg, companyId, tokensUsed, "text", null, "FileAnalytics");

                return new
                {
                    message = errorMsg,
                    data = new List<object>(),
                    tokensUsed,
                    conversationId = convId
                };
            }
        }


        //Helper methods from original implementation


        private async Task<List<ChatMessage>> GetLimitedHistoryAsync_up1(
    long conversationId,
    int maxHistory,
    string? userMessage = null,
    string? assistantMessage = null,
    string? chosenService = "SQLAnalytics",
    string? context = null, // Generic context parameter (schema for SQL, document for PDF)
    CancellationToken ct = default)
        {
            var chatHistory = new List<ChatMessage>();

            var sysPrompt = await GetPromptAsync(Convert.ToInt32(User.FindFirstValue("CompanyId")), chosenService);

            // Build the complete system prompt with context
            var systemPromptBuilder = new System.Text.StringBuilder();

            // Add base system prompt
            var baseSystemPrompt = string.Equals(chosenService, "FileAnalytics", StringComparison.OrdinalIgnoreCase)
                ? sysPrompt + "\n\n" + "You are a helpful document assistant. Use the provided document context to answer questions accurately." + "\n\n" + PromptService.Pdfrule
                : sysPrompt + "\n\n" + PromptService.Rules;

            systemPromptBuilder.AppendLine(baseSystemPrompt);

            // Add context if provided
            if (!string.IsNullOrEmpty(context))
            {
                if (string.Equals(chosenService, "FileAnalytics", StringComparison.OrdinalIgnoreCase))
                {
                    systemPromptBuilder.AppendLine("\n### DOCUMENT CONTEXT");
                    systemPromptBuilder.AppendLine(context);
                    systemPromptBuilder.AppendLine("\nIMPORTANT: Use the above document context to provide accurate answers. Cite relevant sources when possible.");
                }
                else
                {
                    systemPromptBuilder.AppendLine("\n### DATABASE SCHEMA CONTEXT");
                    systemPromptBuilder.AppendLine(context);
                    systemPromptBuilder.AppendLine("\nIMPORTANT: Use the above schema to generate accurate SQL queries. Only generate SELECT statements.");
                }
            }

            chatHistory.Add(new ChatMessage(ChatRole.System, systemPromptBuilder.ToString()));

            // Pull last N messages for THIS conversation from your chat store
            if (_chat != null && conversationId > 0)
            {
                var resp = await _chat.GetConversationMessagesAsync(
                    new GetConversationMessages { ConversationId = conversationId });

                var msgs = resp?.Data ?? new List<ChatMessageVm>();

                // Keep only the most recent 'maxHistory' messages
                foreach (var m in msgs.Skip(Math.Max(0, msgs.Count - maxHistory)))
                {
                    var sender = (m.SenderType ?? "").Trim().ToLowerInvariant();
                    var text = m.MessageText ?? string.Empty;

                    // Only add user/assistant to LLM context
                    if (sender == "user") chatHistory.Add(new ChatMessage(ChatRole.User, text));
                    else if (sender == "assistant") chatHistory.Add(new ChatMessage(ChatRole.Assistant, text));
                }
            }

            // Optionally append transient messages for this turn
            if (!string.IsNullOrWhiteSpace(userMessage)) chatHistory.Add(new ChatMessage(ChatRole.User, userMessage));
            if (!string.IsNullOrWhiteSpace(assistantMessage)) chatHistory.Add(new ChatMessage(ChatRole.Assistant, assistantMessage));

            return chatHistory;
        }

        // Add memory caches at the controller level to avoid repeated DB calls
        private static readonly MemoryCache _historyCache = new(new MemoryCacheOptions());

        private static string GetHistoryCacheKey(long conversationId) => $"history_{conversationId}";

        private static void InvalidateHistoryCache(long conversationId)
        {
            if (conversationId > 0)
            {
                _historyCache.Remove(GetHistoryCacheKey(conversationId));
            }
        }
        private async Task<List<ChatMessage>> GetLimitedHistoryAsync_up(
   long conversationId,
   int maxHistory,
   string? userMessage = null,
   string? assistantMessage = null,
   string? chosenService = "SQLAnalytics",
   string? context = null, // Generic context parameter (schema for SQL, document for PDF)
   CancellationToken ct = default)
        {
            var sysPrompt = await GetPromptAsync(Convert.ToInt32(User.FindFirstValue("CompanyId")), chosenService);

            // Build the complete system prompt with context
            var systemPromptBuilder = new System.Text.StringBuilder();

            // Add base system prompt
            var baseSystemPrompt = string.Equals(chosenService, "FileAnalytics", StringComparison.OrdinalIgnoreCase)
                ? sysPrompt + "\n\n" + "You are a helpful document assistant. Use the provided document context to answer questions accurately." + "\n\n" + PromptService.Pdfrule
                : sysPrompt + "\n\n" + PromptService.Rules;

            systemPromptBuilder.AppendLine(baseSystemPrompt);

            // Add context if provided
            if (!string.IsNullOrEmpty(context))
            {
                if (string.Equals(chosenService, "FileAnalytics", StringComparison.OrdinalIgnoreCase))
                {
                    systemPromptBuilder.AppendLine("\n### DOCUMENT CONTEXT");
                    systemPromptBuilder.AppendLine(context);
                    systemPromptBuilder.AppendLine("\nIMPORTANT: Use the above document context to provide accurate answers. Cite relevant sources when possible.");
                }
                else
                {
                    systemPromptBuilder.AppendLine("\n### DATABASE SCHEMA CONTEXT");
                    systemPromptBuilder.AppendLine(context);
                    systemPromptBuilder.AppendLine("\nIMPORTANT: Use the above schema to generate accurate SQL queries. Only generate SELECT statements.");
                }
            }

            var systemMessage = new ChatMessage(ChatRole.System, systemPromptBuilder.ToString());

            var cacheKey = GetHistoryCacheKey(conversationId);
            if (!_historyCache.TryGetValue(cacheKey, out List<ChatMessage> persistedMessages))
            {
                persistedMessages = new List<ChatMessage>();

                if (_chat != null && conversationId > 0)
                {
                    var resp = await _chat.GetConversationMessagesAsync(
                        new GetConversationMessages { ConversationId = conversationId });

                    var msgs = resp?.Data ?? new List<ChatMessageVm>();

                    foreach (var m in msgs)
                    {
                        var sender = (m.SenderType ?? "").Trim().ToLowerInvariant();
                        var text = m.MessageText ?? string.Empty;

                        // Only add user/assistant to LLM context
                        if (sender == "user") persistedMessages.Add(new ChatMessage(ChatRole.User, text));
                        else if (sender == "assistant") persistedMessages.Add(new ChatMessage(ChatRole.Assistant, text));
                    }
                }

                _historyCache.Set(cacheKey, persistedMessages, TimeSpan.FromMinutes(5));
            }

            var history = new List<ChatMessage>(capacity: 1 + Math.Min(maxHistory, persistedMessages.Count) +
                (string.IsNullOrWhiteSpace(userMessage) ? 0 : 1) +
                (string.IsNullOrWhiteSpace(assistantMessage) ? 0 : 1));

            history.Add(systemMessage);

            foreach (var m in persistedMessages.Skip(Math.Max(0, persistedMessages.Count - maxHistory)))
            {
                history.Add(m);
            }

            // Optionally append transient messages for this turn
            if (!string.IsNullOrWhiteSpace(userMessage)) history.Add(new ChatMessage(ChatRole.User, userMessage));
            if (!string.IsNullOrWhiteSpace(assistantMessage)) history.Add(new ChatMessage(ChatRole.Assistant, assistantMessage));

            return history;
        }
        private static readonly MemoryCache _promptCache = new(new MemoryCacheOptions());

        private async Task<string> GetPromptAsync(int companyId, string chosenServices)
        {
            var cacheKey = $"prompt_{companyId}_{chosenServices}";
            if (_promptCache.TryGetValue(cacheKey, out string cachedPrompt))
            {
                return cachedPrompt;
            }

            var svc = (!string.IsNullOrEmpty(chosenServices) && chosenServices == "SQLAnalytics") ? 1 : 2;
            var strData = await _repo.GetPromptCompany(new GetPromptCompanyId { CompanyId = companyId, SvcType = svc });
            var prompt = strData.Data ?? string.Empty;

            _promptCache.Set(cacheKey, prompt, TimeSpan.FromMinutes(10));
            return prompt;
        }
       
        // Generic method to handle assistant responses for both services
        private async Task HandleAssistantResponse(long convId, string text, int companyId, int tokensUsed, string mode, object? jsonData, string service = "SQLAnalytics")
        {
            if (_chat != null && convId > 0)
            {
                var asstResp = await _chat.AppendMessageAsync(new AppendMessage
                {
                    ConversationId = convId,
                    SenderType = "assistant",
                    SenderId = null,
                    Text = text,
                    Json = jsonData?.ToString()
                });

                var metadata = new Dictionary<string, string>
                {
                    ["mode"] = mode,
                    ["service"] = service,
                    ["tokensUsed"] = tokensUsed.ToString()
                };

                await _chat.AddMetadataBulkAsync(asstResp.Data, metadata);

                await _chat.IncrementConversationTokensAsync(new IncrementConversationTokens { ConversationId = convId, Tokens = tokensUsed });
                await _chat.IncrementCompanyTokensAsync(new IncrementCompanyTokens { companyId = companyId, Tokens = tokensUsed });

                InvalidateHistoryCache(convId);
            }
        }

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
        private static string StripSqlFence(string raw)
    => raw.Replace("```sql", "", StringComparison.OrdinalIgnoreCase)
          .Replace("```", "", StringComparison.OrdinalIgnoreCase)
          .Trim();

        private static int EstimateTokens(string? text) => string.IsNullOrEmpty(text) ? 0 : Math.Max(1, text.Length / 4);

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

        private async Task<object> HandleGreetingResponse(string message, long convId, int companyId, SqlVectorStores stores, CancellationToken ct)
        {
            int tokensUsed = 0;
            string? replyTextForTitle = null;
            string? newTitle = null;
            long asstMsgId = 0;

            // Build messages for agent with greeting context
            var historyMessages = await GetLimitedHistoryAsync_up(
                conversationId: convId,
                maxHistory: 20,
                userMessage: message,
                chosenService: "SQLAnalytics",
                ct: ct);

            // Use Agent Framework for greeting response
            var greetingResponse = await _agent.RunAsync(historyMessages);
            var raw = greetingResponse.Text?.Trim() ?? "(no text)";

            if (_chat != null && convId > 0 && !string.IsNullOrWhiteSpace(raw))
            {
                await _chat.AddMetadataAsync(convId, "Reply", raw);
            }

            tokensUsed += EstimateTokens(raw);

            if (string.IsNullOrWhiteSpace(raw))
            {
                var msg = "Model did not return response.";
                await HandleAssistantResponse(convId, msg, companyId, tokensUsed, "text", null, "SQLAnalytics");

                replyTextForTitle = msg;
                if (_chat != null && convId > 0)
                    newTitle = await TryUpdateTitleAsync(convId, message, replyTextForTitle, stores, ct);

                return new
                {
                    message = msg,
                    data = new List<object>(),
                    tokensUsed,
                    conversationId = convId,
                    newTitle
                };
            }

            // Handle text response
            await HandleAssistantResponse(convId, raw, companyId, tokensUsed, "text", null, "SQLAnalytics");

            replyTextForTitle = raw;
            if (_chat != null && convId > 0)
                newTitle = await TryUpdateTitleAsync(convId, message, replyTextForTitle, stores, ct);

            return new
            {
                message = raw,
                tokensUsed,
                conversationId = convId,
                newTitle
            };
        }

        private async Task<string?> TryUpdateTitleAsync(long convId, string userText, string replyText, SqlVectorStores _store , CancellationToken ct)
        {
            try
            {
                var oldTitle = await GetTitleAsync(convId, ct);
                string? newTitle = null;

                if (oldTitle != null && oldTitle == "New chat")
                {
                    newTitle = await GenerateShortTitleAsync(userText, replyText, _store, ct);
                    await _chat.UpdateConversationTitleAsync(new UpdateConversationTitle
                    {
                        ConversationId = convId,
                        Title = newTitle
                    });
                }
                else
                {
                    newTitle = oldTitle;
                }

                return newTitle;
            }
            catch { /* ignore title errors */ }
            return null;
        }

        private async Task<string?> GetTitleAsync(long convId, CancellationToken ct)
        {
            string oldTitle = "";
            try
            {
                if (_chat != null)
                {
                    var result = await _chat.GetConversationTitleAsync(new GetConversationMessages { ConversationId = convId });
                    return result.Data.Title;
                }
            }
            catch { /* ignore title errors */ }
            return null;
        }

        

        // Updated title generation to handle both services
      
        private async Task<string> GenerateShortTitleAsync(string userText, string replyText, object? stores, CancellationToken ct)
        {
            // Small prompt to keep cost low
            var sys = "You create concise chat titles (6-40 chars). No quotes. No trailing punctuation.";

            // Build a minimal chat for the TitleAgent.
            // We don't depend on SqlChatService anymore; we call the agent directly.
            var messages = new List<ChatMessage>
    {
        new ChatMessage(ChatRole.System, sys),
        new ChatMessage(
            ChatRole.User,
            $"User: {userText}\nAssistant: {replyText}\nTitle:")
    };

            var titleRun = await _titleAgent.RunAsync(messages, cancellationToken: ct);
            var rawTitle = titleRun.Text?.Trim();

            if (string.IsNullOrWhiteSpace(rawTitle))
            {
                // Safe fallback if agent didn't respond properly
                var fallback = (userText.Length <= 40 ? userText : userText[..40] + "…").Trim();
                return SanitizeTitle(fallback);
            }

            return SanitizeTitle(rawTitle);
        }

        private string SanitizeTitle(string title)
        {
            title = title.Replace("\r", " ").Replace("\n", " ").Trim().Trim('"', '\'', '`', '.', ',');
            if (title.Length > 60) title = title[..60].Trim();
            return title;
        }

        private async Task<object> ExecuteAndReturnResults(string sql, string dbType, string connStr,
    string databaseName, bool wantsChart, long convId, int companyId, int tokensUsed,SqlVectorStores _store,
    string message, CancellationToken ct)
        {
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
                var err1 = "Unable answer for now ";

                if (_chat != null && convId > 0)
                    await _chat.AppendMessageAsync(new AppendMessage { ConversationId = convId, SenderType = "assistant", SenderId = null, Text = sql +" : "+err });

                InvalidateHistoryCache(convId);

                var replyTextForTitle = err;
                string? newTitle = null;
                if (_chat != null && convId > 0)
                    newTitle = await TryUpdateTitleAsync(convId, message, replyTextForTitle, _store,ct);

                return new
                {
                    message = err1,
                    data = new List<object>(),
                    tokensUsed,
                    conversationId = convId,
                    newTitle
                };
            }

            // Chart branch
            if (wantsChart && table != null && table.Columns.Count > 0 && table.Rows.Count > 0)
            {
                //var (specJson, chartSpecTokens) = await GenerateChartSpecAsync(table, message,_store, ct);
                var (specJson, chartSpecTokens) = await GenerateChartSpecAsync(table, message, ct);
                tokensUsed += chartSpecTokens;

                if (_chat != null && convId > 0)
                {
                    var chartDataJson = System.Text.Json.JsonSerializer.Serialize(DataTableToPlain(table));
                    var chartPayload = new
                    {
                        mode = "chart",
                        spec = System.Text.Json.JsonSerializer.Deserialize<object>(specJson),
                        data = DataTableToPlain(table)
                    };
                    var chartJsonForMessage = System.Text.Json.JsonSerializer.Serialize(chartPayload);

                    var asstResp = await _chat.AppendMessageAsync(new AppendMessage
                    {
                        ConversationId = convId,
                        SenderType = "assistant",
                        SenderId = null,
                        Text = sql,
                        Json = chartJsonForMessage
                    });
                    var asstMsgId = asstResp.Data;

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

                    InvalidateHistoryCache(convId);
                  }

                var replyTextForTitle = $"Chart for {table.Rows.Count} rows";
                string? newTitle = null;
                if (_chat != null && convId > 0)
                    newTitle = await TryUpdateTitleAsync(convId, message, replyTextForTitle,_store, ct);

                return new
                {
                    message = "Here's a chart for your result.",
                    data = rows,
                    chart = new { spec = specJson, data = DataTableToPlain(table) },
                    tokensUsed,
                    conversationId = convId,
                    newTitle
                };
            }

            // Table / plain result
            var okMsg = rows.Count == 0 ? "No data found." : "Here are your results:";
            if (_chat != null && convId > 0)
            {
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

                  var asstResp = await _chat.AppendMessageAsync(new AppendMessage
                  {
                      ConversationId = convId,
                    SenderType = "assistant",
                    SenderId = null,
                    Text = sql,
                    Json = tableJsonForMessage
                });
                var asstMsgId = asstResp.Data;

                  var colJson = System.Text.Json.JsonSerializer.Serialize(table.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
                  await _chat.AddMetadataBulkAsync(asstMsgId, new Dictionary<string, string>
                  {
                      ["mode"] = "table",
                    ["rowCount"] = table.Rows.Count.ToString(),
                    ["columns"] = colJson,
                    ["tableData"] = rowsJson,
                    ["tokensUsed"] = tokensUsed.ToString()
                });

                  await _chat.IncrementConversationTokensAsync(new IncrementConversationTokens { ConversationId = convId, Tokens = tokensUsed });
                  await _chat.IncrementCompanyTokensAsync(new IncrementCompanyTokens { companyId = companyId, Tokens = tokensUsed });

                InvalidateHistoryCache(convId);
              }

            var replyText = rows.Count == 0 ? okMsg :
                $"Table result: {rows.Count} rows";
            string? finalTitle = null;
            if (_chat != null && convId > 0)
                finalTitle = await TryUpdateTitleAsync(convId, message, replyText, _store, ct);

            return new
            {
                message = okMsg,
                data = rows,
                tokensUsed,
                conversationId = convId,
                newTitle = finalTitle
            };
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

        private async Task<(string specJson, int tokens)> GenerateChartSpecAsync(
    DataTable table,
    string message,
    CancellationToken ct)
        {
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

            var instruction = @"
You will receive a table schema and sample rows in JSON.
Respond with a STRICT JSON object for Chart.js with fields:
- type: one of bar|line|pie|doughnut|scatter
- xKey: column name for X axis (prefer date/time or category)
- yKeys: array of 1-3 numeric column names
- title: short chart title
- Do not use markdown formatting or triple backticks.
Return ONLY the JSON (no prose). Choose sensible defaults.";

            var payload = System.Text.Json.JsonSerializer.Serialize(new { schema, sample });
            var nprompt = $"User request: {message}\n{instruction}\n{payload}";

            // Build messages for chart agent
            var chartMessages = new List<ChatMessage>
    {
        new ChatMessage(ChatRole.System, "You are a chart specification generator. Generate valid JSON for Chart.js based on the provided data schema."),
        new ChatMessage(ChatRole.User, nprompt)
    };

            // Use ChartAgent directly
            var chartResponse = await _chartAgent.RunAsync(chartMessages, cancellationToken: ct);
            var spec = chartResponse.Text?.Trim() ?? string.Empty;

            // Clean up the response (remove any markdown fences if present)
            spec = CleanJsonResponse(spec);

            if (string.IsNullOrWhiteSpace(spec) || !IsValidJson(spec))
            {
                // Safe fallback
                var cols = table.Columns.Cast<DataColumn>().ToList();
                var x = cols.FirstOrDefault(c => c.DataType == typeof(string))?.ColumnName ?? cols.First().ColumnName;
                var y = cols.FirstOrDefault(c =>
                            c.DataType == typeof(int) || c.DataType == typeof(long) ||
                            c.DataType == typeof(float) || c.DataType == typeof(double) ||
                            c.DataType == typeof(decimal))?.ColumnName
                        ?? cols.Last().ColumnName;

                spec = System.Text.Json.JsonSerializer.Serialize(new
                {
                    type = "bar",
                    xKey = x,
                    yKeys = new[] { y },
                    title = "Chart"
                });
            }

            int estTokens = (instruction.Length + payload.Length + spec.Length) / 4;
            return (spec, estTokens);
        }

        // Helper method to clean JSON response
        private static string CleanJsonResponse(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return response;

            // Remove markdown code fences
            response = Regex.Replace(response, @"```(json)?\s*", "", RegexOptions.IgnoreCase);
            response = Regex.Replace(response, @"```\s*$", "", RegexOptions.IgnoreCase);

            // Remove any leading/trailing whitespace
            response = response.Trim();

            return response;
        }

        // Helper method to validate JSON
        private static bool IsValidJson(string json)
        {
            try
            {
                System.Text.Json.JsonDocument.Parse(json);
                return true;
            }
            catch
            {
                return false;
            }
        }

    }
}
//}
