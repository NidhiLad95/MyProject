using GenxAi_Solutions_V1.Models;
using GenxAi_Solutions_V1.Services.Interfaces;
using GenxAi_Solutions_V1.Utils;
using Microsoft.Agents.AI;
using Microsoft.Extensions.VectorData;

namespace GenxAi_Solutions_V1.Services
{
    public sealed class SqlChatService(
            //IVectorStoreFactory storefac,
            ChatClientAgent agent,           // Sql agent, see Program.cs
            IChatHistoryService history//,string dbpath
        ) : ISqlChatService
    {
        //private readonly SqlVectorStores _stores;
        //private readonly SqlVectorStores _stores=storefac.Create_New(dbpath);
        private readonly ChatClientAgent _agent=agent;
        private readonly IChatHistoryService _history=history;


        public async Task<ChatResponseDto> AskAsync(string question, string conversationId, SqlVectorStores _store)
        {
            // 1) If repeated question, return from history directly
            var fromHistory = _history.FindPreviousAnswer(conversationId, "sql", question);
            if (fromHistory != null)
            {
                return new ChatResponseDto
                {
                    Answer = fromHistory,
                    ConversationId = conversationId,
                    FromHistory = true
                };
            }

            // 2) Vector search schemas (auto-embeds question)
            var hits = new List<VectorSearchResult<SchemaRecord>>();
            await foreach (var r in _store.Schemas.SearchAsync(question, top: 3))
            {
                hits.Add(r);
            }

            // 3) Fetch SQL rules prompt
            var rules = await _store.Prompts.GetAsync("sql_rules");

            // 4) Build context
            var ctx = new System.Text.StringBuilder();
            foreach (var h in hits)
            {
                ctx.AppendLine(h.Record.SchemaText);
                ctx.AppendLine();
            }

            if (rules is not null)
            {
                ctx.AppendLine("### RULES");
                ctx.AppendLine(rules.Text);
            }

            // 5) Build messages (system + history + user)
            var historyMessages = _history.GetHistory(conversationId, "sql");

            //var systemPrompt =
            //    "You are an assistant that writes ONLY valid T-SQL SELECT queries for SQL Server. " +
            //    "Return ONLY the SQL query, without explanation.";

            var messages = PromptBuilder.BuildMessages(
                question,
                // systemPrompt,
                ctx.ToString(),
                historyMessages);

            // 6) Call agent
            AgentRunResponse response = await _agent.RunAsync(messages);
            var answer = response.Text?.Trim() ?? "(no text)";

            // 7) Save to history
            _history.AddTurn(conversationId, "sql", question, answer);

            return new ChatResponseDto
            {
                Answer = answer,
                ConversationId = conversationId,
                Context = hits
                    .Select(h => new { id = h.Record.Id, name = h.Record.Name, score = h.Score })
                    .Cast<object>()
                    .ToList(),
                FromHistory = false
            };
        }
    }
}
