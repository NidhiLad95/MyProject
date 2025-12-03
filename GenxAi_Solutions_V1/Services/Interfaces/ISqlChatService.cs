using GenxAi_Solutions_V1.Models;
using GenxAi_Solutions_V1.Utils;

namespace GenxAi_Solutions_V1.Services.Interfaces
{
    public interface ISqlChatService
    {
       Task<ChatResponseDto> AskAsync(string question, string conversationId, SqlVectorStores _store);
    }
}
