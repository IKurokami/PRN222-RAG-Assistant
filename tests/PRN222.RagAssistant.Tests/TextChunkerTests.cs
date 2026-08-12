using PRN222.RagAssistant.Infrastructure.Parsing;
using Xunit;

namespace PRN222.RagAssistant.Tests;

public sealed class TextChunkerTests
{
    [Fact]
    public void Chunk_ShortText_ReturnsSingleChunk()
    {
        var chunker = new TextChunker(maxChunkSize: 500, overlapSize: 100);
        var pages = new[]
        {
            new ParsedPage("Short text content for testing.", PageNumber: 1, SlideNumber: null)
        };

        var chunks = chunker.Chunk(pages);

        Assert.Single(chunks);
        Assert.Equal("Short text content for testing.", chunks[0].Content);
        Assert.Equal(1, chunks[0].PageNumber);
        Assert.Null(chunks[0].SlideNumber);
        Assert.Equal(0, chunks[0].ChunkIndex);
    }

    [Fact]
    public void Chunk_LongText_SplitsIntoMultipleChunksWithOverlap()
    {
        var chunker = new TextChunker(maxChunkSize: 50, overlapSize: 10);
        var longText = "First sentence here. Second sentence follows immediately. Third sentence is also present.";
        var pages = new[]
        {
            new ParsedPage(longText, PageNumber: 2, SlideNumber: null)
        };

        var chunks = chunker.Chunk(pages);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.Equal(2, c.PageNumber));
        Assert.Equal(0, chunks[0].ChunkIndex);
        Assert.Equal(1, chunks[1].ChunkIndex);
    }

    [Fact]
    public void Chunk_SlideMetadata_PreservesSlideNumber()
    {
        var chunker = new TextChunker(maxChunkSize: 500, overlapSize: 100);
        var pages = new[]
        {
            new ParsedPage("Slide bullet point text.", PageNumber: null, SlideNumber: 3)
        };

        var chunks = chunker.Chunk(pages);

        Assert.Single(chunks);
        Assert.Null(chunks[0].PageNumber);
        Assert.Equal(3, chunks[0].SlideNumber);
    }
}
