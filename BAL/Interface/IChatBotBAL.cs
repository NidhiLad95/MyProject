using BOL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BAL.Interface
{
    public interface IChatBotBAL
    {
        Task<Response<long>> StartConversationAsync(StartConversation model);
        Task<Response<long>> AppendMessageAsync(AppendMessage model);
        Task<ResponseGetList<ChatHeaderVm>> GetRecentConversationsAsync(GetRecentConversations model);
        Task<ResponseGetList<ChatMessageVm>> GetConversationMessagesAsync(GetConversationMessages model);
        Task IncrementConversationTokensAsync(IncrementConversationTokens model);
        Task<Response<int>> IncrementCompanyTokensAsync(IncrementCompanyTokens model);
        Task AddMetadataAsync(long messageId, string key, string value);
        Task AddMetadataBulkAsync(long messageId, IDictionary<string, string> items);
        Task<Response<long>> UpdateConversationTitleAsync(UpdateConversationTitle model);
        Task<Response<GetTitle>> GetConversationTitleAsync(GetConversationMessages model);

    }
}
