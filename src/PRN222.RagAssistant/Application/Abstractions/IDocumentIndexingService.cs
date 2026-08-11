namespace PRN222.RagAssistant.Application.Abstractions;

/// <summary>
/// Executes the end-to-end indexing pipeline for one persisted document.
/// Implementations own parsing, chunk replacement, embedding, and index-state transitions.
/// </summary>
public interface IDocumentIndexingService
{
    Task IndexAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);
}
