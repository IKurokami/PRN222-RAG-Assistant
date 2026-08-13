using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Infrastructure.Services;

namespace PRN222.RagAssistant.Tests;

public sealed class TextEmbeddingBatcherTests
{
    [Fact]
    public async Task EmbedAsync_UsesBoundedBatchesAndPreservesOrder()
    {
        var embeddingService = new RecordingEmbeddingService();
        var batcher = new TextEmbeddingBatcher(embeddingService);
        var texts = Enumerable.Range(0, 65).Select(index => $"text-{index}").ToArray();

        var embeddings = await batcher.EmbedAsync(texts);

        Assert.Equal([32, 32, 1], embeddingService.BatchSizes);
        Assert.Equal(65, embeddings.Count);
        Assert.Equal(
            Enumerable.Range(0, 65).Select(index => (float)index),
            embeddings.Select(embedding => embedding[0]));
    }

    [Fact]
    public async Task EmbedAsync_RejectsMismatchedBatchResponseCount()
    {
        var batcher = new TextEmbeddingBatcher(new MismatchedEmbeddingService());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => batcher.EmbedAsync(["first", "second"]));

        Assert.Contains("1 vectors for 2 inputs", exception.Message);
    }

    private sealed class RecordingEmbeddingService : ITextEmbeddingService
    {
        private int _nextIndex;

        public List<int> BatchSizes { get; } = [];

        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Batcher should use batch embedding.");

        public Task<IReadOnlyList<float[]>> EmbedBatchAsync(
            IReadOnlyList<string> texts,
            CancellationToken cancellationToken = default)
        {
            BatchSizes.Add(texts.Count);
            IReadOnlyList<float[]> embeddings = texts
                .Select(_ => new[] { (float)_nextIndex++ })
                .ToArray();
            return Task.FromResult(embeddings);
        }
    }

    private sealed class MismatchedEmbeddingService : ITextEmbeddingService
    {
        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default) =>
            Task.FromResult(new[] { 1.0f });

        public Task<IReadOnlyList<float[]>> EmbedBatchAsync(
            IReadOnlyList<string> texts,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<float[]>>([new[] { 1.0f }]);
    }
}
