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

    [Fact]
    public void Chunk_600Chars_ProducesExactlyTwoChunks()
    {
        var chunker = new TextChunker(maxChunkSize: 500, overlapSize: 100);
        var text600 = new string('A', 600);
        var pages = new[]
        {
            new ParsedPage(text600, PageNumber: 1, SlideNumber: null)
        };

        var chunks = chunker.Chunk(pages);

        Assert.Equal(2, chunks.Count);
        Assert.Equal(500, chunks[0].Content.Length);
        Assert.Equal(200, chunks[1].Content.Length);
    }

    [Fact]
    public void Chunk_WordDelimitedText_NeverStartsOrEndsWithPartialWords()
    {
        var vocabulary = new[]
        {
            "alpha", "bravo", "charlie", "delta", "elephant", "foxtrot",
            "gigantic", "hotel", "indigo", "juliet", "kilogram", "lemon"
        };
        var chunker = new TextChunker(maxChunkSize: 40, overlapSize: 10);
        var pages = new[]
        {
            new ParsedPage(string.Join(' ', vocabulary), PageNumber: 1, SlideNumber: null)
        };

        var chunks = chunker.Chunk(pages);

        Assert.True(chunks.Count > 1);
        Assert.All(
            chunks.SelectMany(chunk => chunk.Content.Split(' ', StringSplitOptions.RemoveEmptyEntries)),
            word => Assert.Contains(word, vocabulary));
    }

    [Fact]
    public void Chunk_SentencedText_OverlapsAtLeastOneCompleteSentence()
    {
        var sentences = new[]
        {
            "Alpha topic introduces the first complete idea.",
            "Bravo topic explains the second complete idea.",
            "Charlie topic develops the third complete idea.",
            "Delta topic closes the final complete idea."
        };
        var chunker = new TextChunker(maxChunkSize: 100, overlapSize: 35);
        var pages = new[]
        {
            new ParsedPage(string.Join(' ', sentences), PageNumber: 1, SlideNumber: null)
        };

        var chunks = chunker.Chunk(pages);

        Assert.True(chunks.Count > 1);
        for (var index = 1; index < chunks.Count; index++)
        {
            var previous = chunks[index - 1].Content;
            var current = chunks[index].Content;
            Assert.Contains(
                sentences,
                sentence => previous.Contains(sentence, StringComparison.Ordinal)
                            && current.Contains(sentence, StringComparison.Ordinal));
        }
    }
}
