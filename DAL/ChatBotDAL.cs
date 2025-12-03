using BOL;
using DAL.CrudOperations;
using DAL.Interface;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DAL
{
    public class ChatBotDAL : IChatBotDAL
    {
        private readonly ICRUDOperations _crudHelper;
        private readonly string _cs;

        public ChatBotDAL(ICRUDOperations crudHelper,IConfiguration config)
        {
            _crudHelper = crudHelper;
            _cs= config.GetConnectionString("DefaultConnection");
        }

        public async Task<Response<long>> StartConversationAsync(StartConversation model)
        {
            //            const string sql = @"
            //INSERT INTO dbo.Chat_Header(UserID, Title, TokenConsumed, StartedAt, LastUpdatedAt, IsActive)
            //VALUES(@u, @t, 0, GETDATE(), GETDATE(), 1);
            //SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";

            //            await using var con = new SqlConnection(_cs);
            //            return await con.ExecuteScalarAsync<long>(sql, new { u = userId, t = title });

            try
            {
                return await _crudHelper.Insert<long>("Usp_ChatHeaderStartConversation", model);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task<Response<long>> AppendMessageAsync(AppendMessage model)
        {
            //            const string sql = @"
            //INSERT INTO dbo.Chat_Detail(ConversationID, SenderType, SenderID, MessageText, MessageJSON, CreatedAt, IsActive)
            //VALUES(@c, @st, @sid, @txt, @json, GETDATE(), 1);

            //UPDATE dbo.Chat_Header SET LastUpdatedAt = GETDATE() WHERE ConversationID=@c;";

            //            await using var con = new SqlConnection(_cs);
            //            await con.ExecuteAsync(sql, new { c = conversationId, st = senderType, sid = senderId, txt = text, json });

            try
            {
                return await _crudHelper.Insert<long>("Usp_ChatDetailAppendMessage", model);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task<ResponseGetList<ChatHeaderVm>> GetRecentConversationsAsync(GetRecentConversations model)
        {
            //            const string sql = @"
            //SELECT TOP(@take) ConversationID, ISNULL(Title,'' ) Title, LastUpdatedAt, ISNULL(TokenConsumed,0) TokenConsumed
            //FROM dbo.Chat_Header WITH (NOLOCK)
            //WHERE UserID=@u AND IsActive=1
            //ORDER BY LastUpdatedAt DESC;";

            //            await using var con = new SqlConnection(_cs);
            //            var list = (await con.QueryAsync<ChatHeaderVm>(sql, new { u = userId, take })).ToList();
            //            return list;
            try
            {
                return await _crudHelper.GetList<ChatHeaderVm>("Usp_ChatHeaderGetRecentConversations", model);
            }
            catch (Exception ex)
            {
                throw ex;
            }

        }

        public async Task<ResponseGetList<ChatMessageVm>> GetConversationMessagesAsync(GetConversationMessages model)
        {
            //            const string sql = @"
            //SELECT MessageID, SenderType, ISNULL(MessageText,'') MessageText, CreatedAt
            //FROM dbo.Chat_Detail WITH (NOLOCK)
            //WHERE ConversationID=@c AND IsActive=1
            //ORDER BY CreatedAt ASC;";

            //            await using var con = new SqlConnection(_cs);
            //            var list = (await con.QueryAsync<ChatMessageVm>(sql, new { c = conversationId })).ToList();
            //            return list;
            try
            {
                return await _crudHelper.GetList<ChatMessageVm>("Usp_ChatDetailGetMessages", model);
            }
            catch (Exception ex)
            {
                throw ex;
            }

        }

        public async Task<Response<long>> IncrementConversationTokensAsync(IncrementConversationTokens model)
        {
            //            const string sql = @"
            //UPDATE dbo.Chat_Header
            //SET TokenConsumed = ISNULL(TokenConsumed,0) + @t, LastUpdatedAt = GETDATE()
            //WHERE ConversationID=@c;";

            //            await using var con = new SqlConnection(_cs);
            //            await con.ExecuteAsync(sql, new { c = conversationId, t = tokens
            //            
            try
            {
                return await _crudHelper.Update<long>("Usp_ChatHeaderIncrementTokens", model);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task<Response<int>> IncrementCompanyTokensAsync(IncrementCompanyTokens model)
        {
            //            const string sql = @"
            //UPDATE dbo.CompanyProfile SET TokenUsed = ISNULL(TokenUsed,0) + @t WHERE CompanyID=@cid;
            //SELECT ISNULL(TokenUsed,0) FROM dbo.CompanyProfile WITH (NOLOCK) WHERE CompanyID=@cid;";

            //            await using var con = new SqlConnection(_cs);
            //            return await con.ExecuteScalarAsync<int>(sql, new { cid = companyId, t = tokens });

            try
            {
                return await _crudHelper.Update<int>("Usp_CompanyProfileIncrementTokens", model);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        //public async Task AddMetadataAsync(long messageId, string key, string value)
        //{
        //    const string sql = @"INSERT INTO dbo.Chat_MetaData(MessageID,KeyName,KeyValue,IsActive,CreatedDate)
        //                 VALUES(@m,@k,@v,1,CAST(FORMAT(GETDATE(),'yyyyMMdd') AS INT));";
        //    await using var con = new SqlConnection(_cs);
        //    await con.ExecuteAsync(sql, new { m = messageId, k = key, v = value });
        //}

        //public async Task AddMetadataBulkAsync(long messageId, IDictionary<string, string> items)
        //{
        //    const string sql = @"INSERT INTO dbo.Chat_MetaData(MessageID,KeyName,KeyValue,IsActive,CreatedDate)
        //                 VALUES(@MessageID,@KeyName,@KeyValue,1,CAST(FORMAT(GETDATE(),'yyyyMMdd') AS INT));";
        //    var rows = items.Select(kv => new { MessageID = messageId, KeyName = kv.Key, KeyValue = kv.Value });
        //    await using var con = new SqlConnection(_cs);
        //    await con.ExecuteAsync(sql, rows);
        //}

        // SINGLE item (upgrade existing one to be MAX-safe too)
        public async Task AddMetadataAsync(long messageId, string key, string value)
        {
            const string sql = @"INSERT INTO dbo.Chat_MetaData(MessageID,KeyName,KeyValue,IsActive,CreatedDate)
                         VALUES(@MessageID,@KeyName,@KeyValue,1,CAST(FORMAT(GETDATE(),'yyyyMMdd') AS INT));";

            await using var con = new SqlConnection(_cs);
            await con.OpenAsync();

            var p = new DynamicParameters();
            p.Add("MessageID", messageId, DbType.Int64);
            p.Add("KeyName", key, DbType.String);
            // Force NVARCHAR(MAX) for large JSON
            p.Add("KeyValue", new DbString
            {
                Value = value ?? string.Empty,
                IsAnsi = false,
                IsFixedLength = false,
                Length = int.MaxValue
            });

            await con.ExecuteAsync(sql, p);
        }

        // BULK items (drop-in replacement for your method)
        public async Task AddMetadataBulkAsync(long messageId, IDictionary<string, string> items)
        {
            const string sql = @"INSERT INTO dbo.Chat_MetaData(MessageID,KeyName,KeyValue,IsActive,CreatedDate)
                         VALUES(@MessageID,@KeyName,@KeyValue,1,CAST(FORMAT(GETDATE(),'yyyyMMdd') AS INT));";

            await using var con = new SqlConnection(_cs);
            await con.OpenAsync();
            using var tx = con.BeginTransaction();

            foreach (var kv in items)
            {
                var p = new DynamicParameters();
                p.Add("MessageID", messageId, DbType.Int64);
                p.Add("KeyName", kv.Key, DbType.String);
                // Force NVARCHAR(MAX)
                p.Add("KeyValue", new DbString
                {
                    Value = kv.Value ?? string.Empty,
                    IsAnsi = false,
                    IsFixedLength = false,
                    Length = int.MaxValue
                });

                await con.ExecuteAsync(sql, p, tx);
            }

            tx.Commit();
        }

        public async Task<Response<long>> UpdateConversationTitleAsync(UpdateConversationTitle model)
        {
            try
            {
                // Create a proc named Usp_ChatHeaderUpdateTitle (see SQL at the end)
                return await _crudHelper.Update<long>("Usp_ChatHeaderUpdateTitle", model);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task<Response<GetTitle>> GetConversationTitleAsync(GetConversationMessages model)
        {
            
            try
            {
                return await _crudHelper.GetSingleRecord<GetTitle>("Usp_ChatHeaderGetTitle", model);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

    }
}
