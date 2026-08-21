namespace PRN222.RagAssistant.Infrastructure.Rag;

/// <summary>
/// Pure ranking helper for Agentic RAG hybrid retrieval. Semantic candidates must
/// satisfy the configured similarity threshold before they can contribute evidence;
/// keyword candidates are already constrained by PostgreSQL full-text matching.
/// </summary>
public static class AgenticRetrievalRanker
{
    private const double RrfK = 60.0;

    public static IReadOnlyList<RetrievedChunk> Fuse(
        IReadOnlyList<RetrievedChunk> semantic,
        IReadOnlyList<RetrievedChunk> keyword,
        double minimumSemanticSimilarity,
        int topK)
    {
        topK = Math.Clamp(topK, 1, 12);
        minimumSemanticSimilarity = Math.Clamp(minimumSemanticSimilarity, 0.0, 1.0);

        var fused = new Dictionary<Guid, (RetrievedChunk Chunk, double Score)>();
        var semanticCandidates = semantic
            .Where(chunk => chunk.SimilarityScore >= minimumSemanticSimilarity)
            .ToList();

        AddRrfScores(fused, semanticCandidates, topK * 2);
        AddRrfScores(fused, keyword, topK * 2);

        return fused.Values
            .OrderByDescending(item => item.Score)
            .Take(topK)
            .Select(item => item.Chunk with { SimilarityScore = item.Score })
            .ToList();
    }

    private static void AddRrfScores(
        IDictionary<Guid, (RetrievedChunk Chunk, double Score)> fused,
        IReadOnlyList<RetrievedChunk> chunks,
        int maxItems)
    {
        for (var index = 0; index < Math.Min(chunks.Count, maxItems); index++)
        {
            var chunk = chunks[index];
            var contribution = 1.0 / (RrfK + index + 1);

            if (fused.TryGetValue(chunk.DocumentChunkId, out var existing))
            {
                fused[chunk.DocumentChunkId] = (existing.Chunk, existing.Score + contribution);
            }
            else
            {
                fused[chunk.DocumentChunkId] = (chunk, contribution);
            }
        }
    }
}
