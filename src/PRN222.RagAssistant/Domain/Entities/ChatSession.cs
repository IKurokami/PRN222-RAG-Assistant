namespace PRN222.RagAssistant.Domain.Entities;

public sealed class ChatSession
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid? SubjectId { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
