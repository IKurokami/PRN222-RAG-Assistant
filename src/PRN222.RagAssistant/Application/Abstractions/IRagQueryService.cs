using PRN222.RagAssistant.Application.Models;

namespace PRN222.RagAssistant.Application.Abstractions;

/// <summary>
/// Application boundary used by chat and evaluation presentation layers.
/// Persisted chat queries validate session ownership and store the exchange;
/// stateless queries are intended for isolated evaluation runs.
/// </summary>
public interface IRagQueryService
{
    Task<RagAnswer> AskAsync(
        Guid userId,
        Guid chatSessionId,
        string question,
        Guid? subjectId = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<RagStreamEvent> AskStreamAsync(
        Guid userId,
        Guid chatSessionId,
        string question,
        Guid? subjectId = null,
        CancellationToken cancellationToken = default);

    Task<RagQueryResult> AskStatelessAsync(
        string question,
        Guid subjectId,
        CancellationToken cancellationToken = default);

    Task<Guid> GetOrCreateUserSessionAsync(
        Guid userId,
        Guid? subjectId = null,
        CancellationToken cancellationToken = default);
}
