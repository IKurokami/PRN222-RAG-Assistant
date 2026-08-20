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

/// <summary>
/// RAG answer generated without loading or persisting chat history.
/// </summary>
public sealed record RagQueryResult(
    string Answer,
    IReadOnlyList<RagCitation> Citations);
