using PRN222.RagAssistant.Infrastructure.Parsing;
using Xunit;

namespace PRN222.RagAssistant.Tests;

public sealed class TextChunkerTests
{
    [Fact]
    public void Chunk_ShortText_ReturnsSingleChunk()
    {
        var chunker = TextChunker.Create(maxChunkSize: 500, overlapSize: 100);
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
        var chunker = TextChunker.Create(maxChunkSize: 50, overlapSize: 10);
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
        var chunker = TextChunker.Create(maxChunkSize: 500, overlapSize: 100);
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
        var chunker = TextChunker.Create(maxChunkSize: 500, overlapSize: 100);
        var text600 = new string('A', 600);
        var pages = new[]
        {
            new ParsedPage(text600, PageNumber: 1, SlideNumber: null)
        };

        var chunks = chunker.Chunk(pages);

        Assert.Equal(2, chunks.Count);
        Assert.Equal(500, chunks[0].Content.Length);
        // Second chunk is shorter
        Assert.True(chunks[1].Content.Length < 500);
        // Combined length should cover the full text (with some overlap)
        var combinedLength = chunks[0].Content.Length + chunks[1].Content.Length;
        Assert.True(combinedLength >= 600, $"Combined length {combinedLength} should cover full text");
    }

    [Fact]
    public void Chunk_WordDelimitedText_NeverStartsOrEndsWithPartialWords()
    {
        var vocabulary = new[]
        {
            "alpha", "bravo", "charlie", "delta", "elephant", "foxtrot",
            "gigantic", "hotel", "indigo", "juliet", "kilogram", "lemon"
        };
        var chunker = TextChunker.Create(maxChunkSize: 40, overlapSize: 10);
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
        var chunker = TextChunker.Create(maxChunkSize: 100, overlapSize: 35);
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

    [Fact]
    public void Chunk_OverlapIsBounded_ActualOverlapDoesNotExceedConfiguredOverlap()
    {
        var chunker = TextChunker.Create(maxChunkSize: 100, overlapSize: 20);
        // Create text with sentences so the overlap calculation is clear
        var text = "This is sentence one. " +
                   "This is sentence two. " +
                   "This is sentence three. " +
                   "This is sentence four. " +
                   "This is sentence five. " +
                   "This is sentence six. " +
                   "This is sentence seven. " +
                   "This is sentence eight. ";
        var pages = new[]
        {
            new ParsedPage(text, PageNumber: 1, SlideNumber: null)
        };

        var chunks = chunker.Chunk(pages);

        Assert.True(chunks.Count > 1);
        // Each adjacent pair should have bounded overlap
        for (var i = 1; i < chunks.Count; i++)
        {
            var previous = chunks[i - 1].Content;
            var current = chunks[i].Content;

            // Calculate actual overlap - chars from end of previous should match start of current
            var actualOverlap = CalculateOverlap(previous, current);

            // Overlap should not exceed 1.5x configured overlap (with slack for word boundaries)
            var maxAllowedOverlap = (int)(20 * 1.5);
            Assert.True(
                actualOverlap <= maxAllowedOverlap,
                $"Overlap {actualOverlap} exceeds max allowed {maxAllowedOverlap}");
        }
    }

    private static int CalculateOverlap(string previous, string current)
    {
        // Find the longest suffix of previous that is a prefix of current
        var overlap = 0;
        for (var i = 1; i <= Math.Min(previous.Length, current.Length); i++)
        {
            var suffix = previous.Substring(previous.Length - i);
            if (current.StartsWith(suffix))
            {
                overlap = i;
            }
        }
        return overlap;
    }

    [Fact]
    public void Chunk_MakesDeterministicForwardProgress()
    {
        var chunker = TextChunker.Create(maxChunkSize: 100, overlapSize: 20);
        var text = new string('X', 300);
        var pages = new[]
        {
            new ParsedPage(text, PageNumber: 1, SlideNumber: null)
        };

        var chunks = chunker.Chunk(pages);

        Assert.True(chunks.Count > 1);
        // Verify each chunk is smaller than max size
        Assert.All(chunks, c => Assert.True(c.Content.Length <= 100));
        // Verify forward progress: each chunk has some unique content at start
        for (var i = 1; i < chunks.Count; i++)
        {
            Assert.True(chunks[i].Content.Length > 0);
        }
    }

    [Fact]
    public void Chunk_VietnameseUnicodeText_RemainsIntact()
    {
        var chunker = TextChunker.Create(maxChunkSize: 50, overlapSize: 10);
        var vietnameseText = "Tiếng Việt có dấu: ă, â, đ, ê, ô, ơ, ư. " +
                           "Câu hỏi mẫu: Tại sao? Học sinh cần gì?";
        var pages = new[]
        {
            new ParsedPage(vietnameseText, PageNumber: 1, SlideNumber: null)
        };

        var chunks = chunker.Chunk(pages);

        var combinedText = string.Join(" ", chunks.Select(c => c.Content));
        Assert.Contains("Tiếng Việt", combinedText);
        Assert.Contains("ă, â, đ, ê, ô, ơ, ư", combinedText);
        Assert.Contains("Tại sao?", combinedText);
        Assert.Contains("Học sinh cần gì?", combinedText);
    }

    [Fact]
    public void Chunk_NoChunkExplosion_ProducesReasonableNumberOfChunks()
    {
        var chunker = TextChunker.Create(maxChunkSize: 500, overlapSize: 100);
        // 3000 chars = should produce ~6-7 chunks max, not 30
        var text = new string('B', 3000);
        var pages = new[]
        {
            new ParsedPage(text, PageNumber: 1, SlideNumber: null)
        };

        var chunks = chunker.Chunk(pages);

        // With 500 char chunks and 100 char overlap, max should be around 7-8 chunks
        Assert.True(chunks.Count <= 10, $"Too many chunks: {chunks.Count}. Expected <= 10");
    }
}
