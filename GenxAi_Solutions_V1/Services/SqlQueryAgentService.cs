//using GenxAi_Solutions_V1.Services.Interfaces;
//using GenxAi_Solutions_V1.Utils;
//using Microsoft.Agents.AI;
//using Microsoft.Extensions.AI;
//using Microsoft.SemanticKernel.Connectors.SqliteVec;

//namespace GenxAi_Solutions_V1.Services
//{
//    public sealed class SqlQueryAgentService
//    {
//        private readonly ChatClientAgent _sqlAgent;
//        private readonly SQLiteVetorStores _store;

//        public SqlQueryAgentService(ChatClientAgent sqlAgent, IVectorStoreFactory storeFactory)
//        {
//            _sqlAgent = sqlAgent;
//            _store = storeFactory.Create("db_ai_default.db"); // swap to company-specific name as you already do
//        }

//        public async Task<string> GenerateQueryAsync(string userQuestion, int topSchemas = 6)
//        {
//            var schemas = _store.GetCollection<string, VectorDBSchemaRecord>("sql_schemas");
//            await schemas.EnsureCollectionExistsAsync();

//            var options = new SqliteCollectionOptions(); // no embeddings needed for string filters here
//            var schemaCol = new SqliteCollection<string, VectorDBSchemaRecord>(_store.ConnectionString, "sql_schemas", options);

//            // If you kept embeddings for schemas, switch to VectorizedSearchAsync and pass EmbeddingGenerator like the PDF path.
//            var top = await schemaCol.GetAllAsync(topSchemas); // use your existing retrieval or vector search

//            var ctx = string.Join("\n---\n", top.Select(s => s.SchemaText));
//            var messages = new List<Microsoft.Extensions.AI.ChatMessage>
//        {
//            new(ChatRole.System, "Return ONLY one valid T-SQL SELECT for SQL Server. No explanation."),
//            new(ChatRole.User, $"Question: {userQuestion}\n\nSchemas:\n{ctx}")
//        };

//            var reply = await _sqlAgent.GetResponseAsync(messages);
//            return reply.Message?.Content?.ToString() ?? "";
//        }
//    }
//}
