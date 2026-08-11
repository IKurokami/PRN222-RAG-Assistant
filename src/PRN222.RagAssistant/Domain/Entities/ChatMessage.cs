using PRN222.RagAssistant.Domain.Enums;

namespace PRN222.RagAssistant.Domain.Entities;

public sealed class ChatMessage
{
    public Guid Id { get; set; }

    public Guid ChatSessionId { get; set; }

    public ChatMessageRole Role { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}
