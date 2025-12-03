using BAL.Interface;
using BOL;
using DAL.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BAL
{
    public class ChatBotBAL : IChatBotBAL
    {
        private readonly IChatBotDAL _DALHelper;
        public ChatBotBAL(IChatBotDAL DALHelper)
        { 
            _DALHelper = DALHelper;
        }

        public Task<Response<long>> AppendMessageAsync(AppendMessage model)
        {
            try
            {
                return _DALHelper.AppendMessageAsync(model);
            }
            catch(Exception ex)
            {
                throw ex;
            }
        }

        public Task<ResponseGetList<ChatMessageVm>> GetConversationMessagesAsync(GetConversationMessages model)
        {
            try
            {
                return _DALHelper.GetConversationMessagesAsync(model);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public Task<ResponseGetList<ChatHeaderVm>> GetRecentConversationsAsync(GetRecentConversations model)
        {
            try
            {
                return _DALHelper.GetRecentConversationsAsync(model);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public Task<Response<int>> IncrementCompanyTokensAsync(IncrementCompanyTokens model)
        {
            try
            {
                return _DALHelper.IncrementCompanyTokensAsync(model);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public Task IncrementConversationTokensAsync(IncrementConversationTokens model)
        {
            try
            {
                return _DALHelper.IncrementConversationTokensAsync(model);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public Task<Response<long>> StartConversationAsync(StartConversation model)
        {
            try
            {
                return _DALHelper.StartConversationAsync(model);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public Task AddMetadataAsync(long messageId, string key, string value)
        => _DALHelper.AddMetadataAsync(messageId, key, value);

        public Task AddMetadataBulkAsync(long messageId, IDictionary<string, string> items)
            => _DALHelper.AddMetadataBulkAsync(messageId, items);

        public Task<Response<long>> UpdateConversationTitleAsync(UpdateConversationTitle model)
        {
            try { return _DALHelper.UpdateConversationTitleAsync(model); }
            catch (Exception ex) { throw ex; }
        }

        public async Task<Response<GetTitle>> GetConversationTitleAsync(GetConversationMessages model)
        {

            try
            {
                return await _DALHelper.GetConversationTitleAsync( model);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

    }
}
