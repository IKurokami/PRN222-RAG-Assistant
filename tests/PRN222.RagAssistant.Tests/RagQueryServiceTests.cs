using PRN222.RagAssistant.Infrastructure.Rag;
using Xunit;

namespace PRN222.RagAssistant.Tests;

public sealed class RagQueryServiceTests
{
    [Fact]
    public void RetrievedChunk_Record_HoldsAllProperties()
    {
        var chunkId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var chunk = new RetrievedChunk(
            DocumentChunkId: chunkId,
            DocumentId: docId,
            DocumentTitle: "Test Document",
            Content: "OOP content here",
            PageNumber: 5,
            SlideNumber: null,
            SimilarityScore: 0.95);

        Assert.Equal(chunkId, chunk.DocumentChunkId);
        Assert.Equal(docId, chunk.DocumentId);
        Assert.Equal("Test Document", chunk.DocumentTitle);
        Assert.Equal("OOP content here", chunk.Content);
        Assert.Equal(5, chunk.PageNumber);
        Assert.Null(chunk.SlideNumber);
        Assert.Equal(0.95, chunk.SimilarityScore);
    }

    [Fact]
    public void ChatHistoryEntry_Record_HoldsAllProperties()
    {
        var entry = new ChatHistoryEntry("User", "What is inheritance?");

        Assert.Equal("User", entry.Role);
        Assert.Equal("What is inheritance?", entry.Content);
    }

    [Theory]
    [InlineData(0.9, 0.3)]
    [InlineData(0.3, 0.3)]
    [InlineData(1.0, 0.3)]
    public void RetrievedChunk_SimilarityScore_IsSetCorrectly(double score, double threshold)
    {
        var chunk = new RetrievedChunk(
            Guid.NewGuid(), Guid.NewGuid(), "Doc", "Content", null, null, score);

        var meetsThreshold = chunk.SimilarityScore >= threshold;
        Assert.True(meetsThreshold);
    }

    [Theory]
    [InlineData(0.29, 0.3)]
    [InlineData(0.1, 0.3)]
    public void RetrievedChunk_SimilarityScore_BelowThreshold(double score, double threshold)
    {
        var chunk = new RetrievedChunk(
            Guid.NewGuid(), Guid.NewGuid(), "Doc", "Content", null, null, score);

        var meetsThreshold = chunk.SimilarityScore >= threshold;
        Assert.False(meetsThreshold);
    }

    [Fact]
    public void RetrievedChunk_Equality_WorksCorrectly()
    {
        var chunkId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var chunk1 = new RetrievedChunk(chunkId, docId, "Doc", "Content", 1, null, 0.9);
        var chunk2 = new RetrievedChunk(chunkId, docId, "Doc", "Content", 1, null, 0.9);

        Assert.Equal(chunk1, chunk2);
    }
}
