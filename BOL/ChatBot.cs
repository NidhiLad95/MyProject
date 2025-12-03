using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BOL
{
    public class ChatDetailDto
    {
        public long MessageID { get; set; }
        public long? ConversationID { get; set; }
        public string? SenderType { get; set; }
        public int? SenderID { get; set; }
        public string? MessageText { get; set; }
        public string? MessageJSON { get; set; }
        public DateTime? CreatedAt { get; set; }
        public int IsActive { get; set; }
        public int? CreatedBy { get; set; }
        public int? CreatedDate { get; set; }   // ⚠️ In DB this is INT, usually should be DateTime. Consider fixing schema.
        public int? MofifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }

    public class ChatHeaderDto
    {
        public long ConversationID { get; set; }
        public int? UserID { get; set; }
        public string? Title { get; set; }
        public int? TokenConsumed { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? LastUpdatedAt { get; set; }
        public int IsActive { get; set; }
        public int? CreatedBy { get; set; }
        public int? CreatedDate { get; set; }   // ⚠️ Same note as above
        public int? MofifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }

    public class ChatMetaDataDto
    {
        public long MetadataID { get; set; }
        public long? MessageID { get; set; }
        public string? KeyName { get; set; }
        public string? KeyValue { get; set; }
        public int IsActive { get; set; }
        public int? CreatedBy { get; set; }
        public int? CreatedDate { get; set; }   // ⚠️ In DB it's INT, usually DateTime expected
        public int? MofifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }

    public sealed class StartChatRequest
    {
        public int CompanyId { get; set; }
        public int UserId { get; set; }
        public string Service { get; set; } = "SQLAnalytics"; // or FileAnalytics
        public string? FirstUserMessage { get; set; }
    }

    public sealed class AskRequest
    {
        public long ConversationId { get; set; }
        public int CompanyId { get; set; }
        public string Service { get; set; } = "SQLAnalytics";
        public string UserMessage { get; set; } = string.Empty;
    }

    public sealed class ChatHeaderVm
    {
        public long ConversationId { get; set; }
        public string Title { get; set; } = "";
        public DateTime LastUpdatedAt { get; set; }
        public int TokenConsumed { get; set; }
    }

    public sealed class ChatMessageVm
    {
        public long MessageId { get; set; }
        public string SenderType { get; set; } = "user"; // user/assistant/system
        public string MessageText { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string? MessageJSON { get; set; }
    }

    public sealed class ChatAskResponse
    {
        public string Mode { get; set; } = "text"; // text | table | chart
        public string AssistantText { get; set; } = "";
        public object? Table { get; set; } // { columns, rows }
        public object? Chart { get; set; } // { spec, data }
        public int TokensUsed { get; set; }
        public int CompanyTokensUsed { get; set; }
    }

    public class StartConversation
    {
        public int UserId { get; set; }
        public string Title { get; set; }
    }

    public class GetTitle
    {
        public int ConversationID { get; set; }
        public string Title { get; set; }
    }

    public class AppendMessage
    {
        public long ConversationId { get; set; }
        public string SenderType { get; set; } = string.Empty;
        public int? SenderId { get; set; }
        public string? Text { get; set; }
        public string? Json { get; set; }
    }

    public class GetRecentConversations 
    {
        public int UserId { get; set; }
        public int Take { get; set; }
    }

    public class GetConversationMessages
    {
        public long ConversationId { get; set; }
    }

    public class IncrementConversationTokens
    {
        public long ConversationId { get; set; }
        public int Tokens { get; set; }
    }

    public class IncrementCompanyTokens
    {
        public int companyId { get; set; }
        public int Tokens { get; set; }
    }
    public sealed class UpdateConversationTitle
    {
        public long ConversationId { get; set; }
        public string Title { get; set; } = "";
    }

}
