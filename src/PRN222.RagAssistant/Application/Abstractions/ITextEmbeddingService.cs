namespace PRN222.RagAssistant.Application.Abstractions;

/// <summary>
/// Produces embeddings without exposing the concrete AI provider to application workflows.
/// The same configured implementation must be used for document indexing and query retrieval.
/// </summary>
public interface ITextEmbeddingService
{
    Task<float[]> EmbedAsync(
        string text,
        CancellationToken cancellationToken = default);
}
