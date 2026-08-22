using System.Runtime.CompilerServices;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Application.Models;

namespace PRN222.RagAssistant.Infrastructure.Rag;

public sealed class QuotaAwareRagQueryService(
    RagQueryService inner,
    IUserQuotaService quotaService) : IRagQueryService
{
    public async Task<RagAnswer> AskAsync(
        Guid userId,
        Guid chatSessionId,
        string question,
        Guid? subjectId = null,
        CancellationToken cancellationToken = default)
    {
        await using var reservation = await quotaService.ReserveQuotaAsync(userId, cancellationToken);
        reservation.Activate();
        return await inner.AskAsync(userId, chatSessionId, question, subjectId, cancellationToken);
    }

    public async IAsyncEnumerable<RagStreamEvent> AskStreamAsync(
        Guid userId,
        Guid chatSessionId,
        string question,
        Guid? subjectId = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var reservation = await quotaService.ReserveQuotaAsync(userId, cancellationToken);
        reservation.Activate();

        await foreach (var streamEvent in inner
                           .AskStreamAsync(userId, chatSessionId, question, subjectId, cancellationToken)
                           .WithCancellation(cancellationToken))
        {
            yield return streamEvent;
        }
    }

    public Task<RagQueryResult> AskStatelessAsync(
        string question,
        Guid subjectId,
        CancellationToken cancellationToken = default) =>
        inner.AskStatelessAsync(question, subjectId, cancellationToken);

    public Task<Guid> GetOrCreateUserSessionAsync(
        Guid userId,
        Guid? subjectId = null,
        CancellationToken cancellationToken = default) =>
        inner.GetOrCreateUserSessionAsync(userId, subjectId, cancellationToken);
}
