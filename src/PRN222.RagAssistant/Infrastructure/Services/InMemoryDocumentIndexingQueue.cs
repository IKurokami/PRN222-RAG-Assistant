using System.Threading.Channels;
using PRN222.RagAssistant.Application.Abstractions;

namespace PRN222.RagAssistant.Infrastructure.Services;

/// <summary>
/// Process-local implementation of <see cref="IDocumentIndexingQueue"/> used by the merged
/// document indexing pipeline.
///
/// Document upload/re-index actions enqueue persisted document IDs here and
/// <see cref="DocumentIndexingWorker"/> consumes them in the background.
///
/// This queue is intentionally in-memory rather than a durable external broker. Recovery is
/// based on persisted document state: the worker re-enqueues documents still marked Uploaded
/// or Processing when the application starts.
///
/// Keep parsing, chunking, embedding, and index-state business logic in
/// <see cref="IDocumentIndexingService"/> rather than in this transport class.
/// </summary>
public sealed class InMemoryDocumentIndexingQueue : IDocumentIndexingQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });

    public ValueTask EnqueueAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        _channel.Writer.TryWrite(documentId);
        return ValueTask.CompletedTask;
    }

    public ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }
}
