using GenxAi_Solutions_V1.Models;

namespace GenxAi_Solutions_V1.Services.Interfaces
{
    public interface IPdfChatService
    {
        Task<ChatResponseDto> AskAsync(
            string question,
            string conversationId,
            int topK);
    }
}
