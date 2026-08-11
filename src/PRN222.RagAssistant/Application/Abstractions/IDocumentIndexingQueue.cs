namespace PRN222.RagAssistant.Application.Abstractions;

/// <summary>
/// Coordinates document indexing work between the upload workflow and the background indexer.
/// </summary>
public interface IDocumentIndexingQueue
{
    ValueTask EnqueueAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);

    ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken);
}
