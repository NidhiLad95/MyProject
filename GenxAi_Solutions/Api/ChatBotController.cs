using BAL.Interface;
using BOL;
using GenxAi_Solutions.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Text.RegularExpressions;

namespace GenxAi_Solutions.Api
{
    [ApiController]
    [Route("api/chat")]
    public class ChatBotController : ControllerBase
    {
        
        private readonly IChatBotBAL _chat;
        private readonly ISqlConfigRepository _configRepo;
        private readonly IVectorSemanticService _fileQa; // your PDF Q&A
        private readonly ILogger<ChatBotController> _log;

        public ChatBotController(IChatBotBAL chat, ISqlConfigRepository configRepo, IVectorSemanticService fileQa, ILogger<ChatBotController> log)
        {
            _chat = chat;
            _configRepo = configRepo;
            _fileQa = fileQa;
            _log = log;
        }

        [HttpGet("services")]
        public async Task<IActionResult> GetServices([FromQuery] int companyId, CancellationToken ct)
        {
            var sqlCfg = await _configRepo.GetByCompanyIdAsync(companyId, ct);
            var files = await _configRepo.GetFilesByCompanyIdAsync(companyId, ct);
            return Ok(new { hasSql = (sqlCfg != null && !string.IsNullOrWhiteSpace(sqlCfg.ConnectionString)), hasFile = files?.Count > 0 });
        }

        [HttpGet("history")]
        public async Task<IActionResult> History([FromQuery] int userId)
            => Ok(await _chat.GetRecentConversationsAsync(new GetRecentConversations { UserId=userId,Take=5}));

        [HttpGet("{conversationId:long}/messages")]
        public async Task<IActionResult> Messages([FromRoute] long conversationId)
            => Ok(await _chat.GetConversationMessagesAsync(new GetConversationMessages { ConversationId = conversationId }));

        [HttpPost("start")]
        public async Task<IActionResult> Start([FromBody] StartChatRequest req)
        {
            string title = string.IsNullOrWhiteSpace(req.FirstUserMessage) ? "New chat"
                     : (req.FirstUserMessage.Length > 40 ? req.FirstUserMessage[..40] + "…" : req.FirstUserMessage);

            var convId = await _chat.StartConversationAsync(new StartConversation { UserId = req.UserId, Title = title });
            await _chat.AppendMessageAsync(new AppendMessage { ConversationId = convId.Data, SenderType = "system", SenderId = null, Text = $"service={req.Service}" });
            if (!string.IsNullOrWhiteSpace(req.FirstUserMessage))
                await _chat.AppendMessageAsync(new AppendMessage { ConversationId = convId.Data, SenderType = "user", SenderId = req.UserId, Text = req.FirstUserMessage });

            return Ok(new { conversationId = convId, title });
        }

        //[HttpPost("ask")]
        //public async Task<IActionResult> Ask([FromBody] AskRequest req, CancellationToken ct)
        //{
        //    var uid = HttpContext?.Session?.GetInt32("UserId") ?? 0;
        //    await _chat.AppendMessageAsync(new AppendMessage { ConversationId = req.ConversationId, SenderType = "user", SenderId = uid, Text = req.UserMessage });

        //    // Determine availability
        //    var sqlCfg = await _configRepo.GetByCompanyIdAsync(req.CompanyId, ct);
        //    var files = await _configRepo.GetFilesByCompanyIdAsync(req.CompanyId, ct);
        //    bool hasSql = sqlCfg != null && !string.IsNullOrWhiteSpace(sqlCfg.ConnectionString);
        //    bool hasFile = files?.Count > 0;

        //    string chosen = req.Service;
        //    if (chosen == "SQLAnalytics" && !hasSql) chosen = hasFile ? "FileAnalytics" : "SQLAnalytics";
        //    if (chosen == "FileAnalytics" && !hasFile) chosen = hasSql ? "SQLAnalytics" : "FileAnalytics";

        //    var wantsChart = Regex.IsMatch(req.UserMessage, @"\b(chart|graph|plot|visual(ize|isation|ization)?)\b", RegexOptions.IgnoreCase);

        //    ChatAskResponse payload;

        //    if (chosen == "FileAnalytics")
        //    {
        //        // Uses your VectorSemanticService.QueryAsync
        //        var qa = await _fileQa.QueryAsync(req.UserMessage, topSections: 30, topChunks: 5);
        //        payload = new ChatAskResponse
        //        {
        //            Mode = "text",
        //            AssistantText = qa.Answer ?? "(no answer)",
        //            TokensUsed = qa.TokensUsed ?? 0
        //        };
        //    }
        //    else
        //    {
        //        // SQLAnalytics path:
        //        // 1) Use your existing prompt→SQL code (you likely generate SQL elsewhere)
        //        //    Replace GetSqlForQuestionAsync with your own method
        //        var (sql, sqlTokens) = await GetSqlForQuestionAsync(req.UserMessage, req.CompanyId, ct);

        //        // 2) Execute the SQL and get a DataTable
        //        var (table, execTokens) = await ExecuteSqlToDataTableAsync(sqlCfg!.ConnectionString!, sql, ct);

        //        if (wantsChart && table != null)
        //        {
        //            // 3) Ask LLM for a chart spec (we’ll add helper right below)
        //            var (specJson, chartTokens) = await GetChartSpecAsync(table);
        //            payload = new ChatAskResponse
        //            {
        //                Mode = "chart",
        //                AssistantText = "Here’s a chart for your result.",
        //                Chart = new { spec = specJson, data = DataTableToPlain(table) },
        //                TokensUsed = sqlTokens + execTokens + chartTokens
        //            };
        //        }
        //        else
        //        {
        //            payload = new ChatAskResponse
        //            {
        //                Mode = "table",
        //                AssistantText = "Here are the results.",
        //                Table = DataTableToPlain(table),
        //                TokensUsed = sqlTokens + execTokens
        //            };
        //        }
        //    }

        //    // persist assistant
        //    var assistantTextDb = payload.Mode switch
        //    {
        //        "text" => payload.AssistantText,
        //        "table" => "[table result]",
        //        "chart" => "[chart result]",
        //        _ => payload.AssistantText
        //    };
        //    var msgJson = payload.Mode == "text" ? null : JsonSerializer.Serialize(new { mode = payload.Mode });
        //    await _chat.AppendMessageAsync(req.ConversationId, "assistant", null, assistantTextDb, msgJson);

        //    // tokens: conversation + company
        //    await _chat.IncrementConversationTokensAsync(req.ConversationId, payload.TokensUsed);
        //    payload.CompanyTokensUsed = await _chat.IncrementCompanyTokensAsync(req.CompanyId, payload.TokensUsed);

        //    return Ok(payload);
        //}

        // ========= Helpers (plug in your own SQL + OpenAI glue) =========
        private async Task<(string sql, int tokens)> GetSqlForQuestionAsync(string userMessage, int companyId, CancellationToken ct)
        {
            // TODO: wire to your existing SQL prompt/embedding generator.
            // TEMP stub:
            await Task.CompletedTask;
            return ($"/* TODO generated */ SELECT TOP 10 * FROM sys.objects", 100);
        }

        private static async Task<(DataTable table, int tokens)> ExecuteSqlToDataTableAsync(string cs, string sql, CancellationToken ct)
        {
            var dt = new DataTable();
            await using var con = new Microsoft.Data.SqlClient.SqlConnection(cs);
            await using var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, con);
            await con.OpenAsync(ct);
            using var rdr = await cmd.ExecuteReaderAsync(ct);
            dt.Load(rdr);
            return (dt, 0);
        }

        //private async Task<(string specJson, int tokens)> GetChartSpecAsync(DataTable table)
        //{
        //    // You can move this to VectorSemanticService if you prefer.
        //    // Provide schema + first 20 rows as JSON to your LLM and return strict JSON spec for Chart.js (type, xKey, yKeys, title).
        //    // TEMP stub (bar over 1st string col vs 1st numeric col)
        //    await Task.CompletedTask;
        //    var cols = table.Columns.Cast<DataColumn>().ToList();
        //    var x = cols.FirstOrDefault(c => c.DataType == typeof(string))?.ColumnName ?? cols[0].ColumnName;
        //    var y = cols.FirstOrDefault(c => c.DataType == typeof(int) || c.DataType == typeof(double) || c.DataType == typeof(decimal))?.ColumnName
        //            ?? cols.Last().ColumnName;
        //    var spec = JsonSerializer.Serialize(new { type = "bar", xKey = x, yKeys = new[] { y }, title = "Chart" });
        //    return (spec, 50);
        //}

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
    }
}
//}
