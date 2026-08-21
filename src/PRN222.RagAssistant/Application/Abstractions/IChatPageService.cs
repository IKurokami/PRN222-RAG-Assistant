using PRN222.RagAssistant.Application.Models;

namespace PRN222.RagAssistant.Application.Abstractions;

public interface IChatPageService
{
    Task<ChatPageSnapshot> GetPageAsync(
        Guid userId,
        Guid? subjectId,
        Guid? sessionId,
        CancellationToken cancellationToken = default);

    Task<Guid> CreateSessionAsync(
        Guid userId,
        Guid subjectId,
        CancellationToken cancellationToken = default);

    Task DeleteSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default);
}
