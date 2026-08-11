namespace PRN222.RagAssistant.Application.Models;

/// <summary>
/// Persisted RAG answer returned to the presentation layer.
/// </summary>
public sealed record RagAnswer(
    Guid ChatSessionId,
    Guid UserMessageId,
    Guid AssistantMessageId,
    string Answer,
    IReadOnlyList<RagCitation> Citations);
