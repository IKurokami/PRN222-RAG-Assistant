using PRN222.RagAssistant.Domain.Enums;

namespace PRN222.RagAssistant.Application.Models;

public sealed class ChatPageSnapshot
{
    public IReadOnlyList<ChatSubjectItem> Subjects { get; init; } = Array.Empty<ChatSubjectItem>();
    public ChatSubjectItem? SelectedSubject { get; init; }
    public IReadOnlyList<ChatSessionItem> Sessions { get; init; } = Array.Empty<ChatSessionItem>();
    public ChatSessionItem? ActiveSession { get; init; }
    public IReadOnlyList<ChatMessageItem> Messages { get; init; } = Array.Empty<ChatMessageItem>();
}

public sealed class ChatSubjectItem
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

public sealed class ChatSessionItem
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class ChatMessageItem
{
    public Guid Id { get; init; }
    public ChatMessageRole Role { get; init; }
    public string Content { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public IReadOnlyList<ChatCitationItem> Citations { get; set; } = Array.Empty<ChatCitationItem>();
}

public sealed class ChatCitationItem
{
    public Guid ChatMessageId { get; init; }
    public int Rank { get; init; }
    public string DocumentTitle { get; init; } = string.Empty;
    public int PageNumber { get; init; }
    public string ChunkContent { get; init; } = string.Empty;
}
