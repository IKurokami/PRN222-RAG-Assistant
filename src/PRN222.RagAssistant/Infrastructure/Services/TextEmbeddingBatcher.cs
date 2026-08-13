using PRN222.RagAssistant.Application.Abstractions;

namespace PRN222.RagAssistant.Infrastructure.Services;

public sealed class TextEmbeddingBatcher
{
    public const int DefaultBatchSize = 32;

    private readonly ITextEmbeddingService _embeddingService;

    public TextEmbeddingBatcher(ITextEmbeddingService embeddingService)
    {
        _embeddingService = embeddingService;
    }

    public async Task<IReadOnlyList<float[]>> EmbedAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        var allEmbeddings = new List<float[]>(texts.Count);

        foreach (var batch in texts.Chunk(DefaultBatchSize))
        {
            var embeddings = await _embeddingService.EmbedBatchAsync(batch, cancellationToken);

            if (embeddings.Count != batch.Length)
            {
                throw new InvalidOperationException(
                    $"Embedding service returned {embeddings.Count} vectors for {batch.Length} inputs.");
            }

            allEmbeddings.AddRange(embeddings);
        }

        return allEmbeddings;
    }
}
