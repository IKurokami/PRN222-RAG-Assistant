using System.Threading.Channels;
using PRN222.RagAssistant.Application.Abstractions;

namespace PRN222.RagAssistant.Infrastructure.Services;

/// <summary>
/// TEMPORARY INTEGRATION STUB — Member 2 only.
///
/// This in-memory implementation of <see cref="IDocumentIndexingQueue"/> exists solely
/// so that the document-management upload flow can be developed and tested independently
/// before Member 3's real queue implementation and background worker are merged.
///
/// Ownership: <see cref="IDocumentIndexingQueue"/> implementation and the hosted background
/// worker belong to Member 3. When Member 3's implementation is merged, this stub must be
/// removed and the DI registration in <c>ServiceCollectionExtensions</c> replaced.
///
/// Do NOT add indexing, parsing, chunking, or Ollama calls here.
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
