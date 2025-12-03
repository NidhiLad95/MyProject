using System.ComponentModel.DataAnnotations;

namespace GenxAi_Solutions_V1.Models
{
    public sealed class ChatRequest
    {
        [Required]
        public string message { get; set; } = string.Empty;

        // For history tracking (you can send from JS, or we fallback to session id)
        public int? ConversationId { get; set; }        
        public string? service { get; set; }        

        public int TopK { get; set; } = 3;
    }

    public sealed class ChatTurn
    {
        public string User { get; set; } = string.Empty;
        public string Assistant { get; set; } = string.Empty;
        public string SystemRole { get; set; } = string.Empty;
    }

    public sealed class ChatResponseDto
    {
        public string Answer { get; set; } = string.Empty;
        public List<object> Context { get; set; } = new();
        public string ConversationId { get; set; } = string.Empty;
        public bool FromHistory { get; set; }
    }
}
