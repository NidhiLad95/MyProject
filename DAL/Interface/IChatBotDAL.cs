using BOL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Interface
{
    public interface IChatBotDAL
    {
        Task<Response<long>> StartConversationAsync(StartConversation model);
        Task<Response<long>> AppendMessageAsync(AppendMessage model);
        Task<ResponseGetList<ChatHeaderVm>> GetRecentConversationsAsync(GetRecentConversations model);
        Task<ResponseGetList<ChatMessageVm>> GetConversationMessagesAsync(GetConversationMessages model);
        Task<Response<long>> IncrementConversationTokensAsync(IncrementConversationTokens model);
        Task<Response<int>> IncrementCompanyTokensAsync(IncrementCompanyTokens model);
        Task<Response<long>> UpdateConversationTitleAsync(UpdateConversationTitle model);
        // CHANGE: return the new MessageID when we append
        //Task<long> AppendMessageAsync(long conversationId, string senderType, int? senderId, string? text, string? json = null);

        // NEW: metadata
        Task AddMetadataAsync(long messageId, string key, string value);
        Task AddMetadataBulkAsync(long messageId, IDictionary<string, string> items);

        //Task IncrementCompanyTokensAsync(int companyId, int tokens);

        Task<Response<GetTitle>> GetConversationTitleAsync(GetConversationMessages model);
    }

}
