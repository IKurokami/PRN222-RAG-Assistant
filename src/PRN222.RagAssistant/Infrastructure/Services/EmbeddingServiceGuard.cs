namespace PRN222.RagAssistant.Infrastructure.Services;

internal static class EmbeddingServiceGuard
{
    public static int GetExpectedDimensions(IConfiguration configuration)
    {
        var rawValue = configuration["Rag:EmbeddingDimensions"];

        if (!int.TryParse(rawValue, out var dimensions) || dimensions <= 0)
        {
            throw new InvalidOperationException(
                "Rag:EmbeddingDimensions must be configured as a positive integer.");
        }

        return dimensions;
    }

    public static IReadOnlyList<float[]> ValidateResponse(
        string providerName,
        IReadOnlyList<float[]> embeddings,
        int expectedCount,
        int expectedDimensions)
    {
        if (embeddings.Count != expectedCount)
        {
            throw new InvalidOperationException(
                $"{providerName} returned {embeddings.Count} embeddings for {expectedCount} inputs.");
        }

        if (embeddings.Any(embedding => embedding.Length != expectedDimensions))
        {
            throw new InvalidOperationException(
                $"{providerName} returned an embedding dimension that does not match Rag:EmbeddingDimensions={expectedDimensions}.");
        }

        return embeddings;
    }
}
