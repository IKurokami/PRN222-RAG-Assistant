namespace PRN222.RagAssistant.Infrastructure.Rag;

/// <summary>
/// Internal interface for retrieving document chunks based on vector similarity.
/// Only RagQueryService depends on this.
/// </summary>
public interface IDocumentChunkRetriever
{
    Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        float[] questionEmbedding,
        Guid subjectId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A document chunk returned by the retriever, ready to be placed into a prompt.
/// </summary>
public sealed record RetrievedChunk(
    Guid DocumentChunkId,
    Guid DocumentId,
    string DocumentTitle,
    string Content,
    int? PageNumber,
    int? SlideNumber,
    double SimilarityScore);

/// <summary>
/// A single turn from the chat history included in the prompt.
/// </summary>
public sealed record ChatHistoryEntry(string Role, string Content);
