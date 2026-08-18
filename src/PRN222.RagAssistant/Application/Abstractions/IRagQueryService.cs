using PRN222.RagAssistant.Application.Models;

namespace PRN222.RagAssistant.Application.Abstractions;

/// <summary>
/// Application boundary used by the chat presentation layer.
/// Implementations must validate session ownership, persist the exchange, and return grounded citations.
/// </summary>
public interface IRagQueryService
{
    Task<RagAnswer> AskAsync(
        Guid userId,
        Guid chatSessionId,
        string question,
        Guid? subjectId = null,
        CancellationToken cancellationToken = default);
}
