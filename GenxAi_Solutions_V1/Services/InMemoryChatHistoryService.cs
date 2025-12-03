using Microsoft.Extensions.AI;
using System.Collections.Concurrent;

namespace GenxAi_Solutions_V1.Services
{
    public sealed class InMemoryChatHistoryService : IChatHistoryService
    {
        private readonly ConcurrentDictionary<string, List<ChatTurn>> _history = new();
        private static string Key(string conv, string channel) => $"{channel}:{conv}";

        public IReadOnlyList<ChatMessage> GetHistory(string conversationId, string channel)
        {
            var k = Key(conversationId, channel);
            if (!_history.TryGetValue(k, out var list)) return Array.Empty<ChatMessage>();
            return list.SelectMany(t => new[] {
            new ChatMessage(ChatRole.User, t.User),
            new ChatMessage(ChatRole.Assistant, t.Assistant)
        }).ToList();
        }

        public void AddTurn(string conversationId, string channel, string user, string assistant)
        {
            var k = Key(conversationId, channel);
            var list = _history.GetOrAdd(k, _ => new());
            list.Add(new ChatTurn(user, assistant));
        }

        public string? FindPreviousAnswer(string conversationId, string channel, string user)
        {
            var k = Key(conversationId, channel);
            if (!_history.TryGetValue(k, out var list)) return null;
            var n = Normalize(user);
            var match = list.LastOrDefault(t => Normalize(t.User) == n);
            return match?.Assistant;
        }

        private static string Normalize(string text) => text.Trim().ToLowerInvariant();
    }

    public record ChatTurn(string User, string Assistant);

    public interface IChatHistoryService
    {
        IReadOnlyList<ChatMessage> GetHistory(string conversationId, string channel);
        void AddTurn(string conversationId, string channel, string user, string assistant);
        string? FindPreviousAnswer(string conversationId, string channel, string user);
    }
}
