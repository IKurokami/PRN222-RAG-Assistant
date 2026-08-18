using Microsoft.Extensions.Options;

namespace PRN222.RagAssistant.Infrastructure.Parsing;

public sealed class TextChunker
{
    private readonly int _maxChunkSize;
    private readonly int _overlapSize;

    public TextChunker(IOptions<ChunkingOptions> options)
    {
        var chunkingOptions = options?.Value ?? new ChunkingOptions();

        if (chunkingOptions.MaxChunkSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkingOptions.MaxChunkSize));
        }

        if (chunkingOptions.OverlapSize < 0 || chunkingOptions.OverlapSize >= chunkingOptions.MaxChunkSize)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkingOptions.OverlapSize));
        }

        _maxChunkSize = chunkingOptions.MaxChunkSize;
        _overlapSize = chunkingOptions.OverlapSize;
    }

    /// <summary>
    /// Creates a TextChunker with explicit values (for testing).
    /// </summary>
    public static TextChunker Create(int maxChunkSize = 500, int overlapSize = 100)
    {
        var options = Options.Create(new ChunkingOptions
        {
            MaxChunkSize = maxChunkSize,
            OverlapSize = overlapSize
        });
        return new TextChunker(options);
    }

    public IReadOnlyList<ChunkedText> Chunk(IReadOnlyList<ParsedPage> pages)
    {
        var chunks = new List<ChunkedText>();
        var chunkIndex = 0;

        foreach (var page in pages)
        {
            var text = NormalizeWhitespace(page.Text);
            if (text.Length == 0)
            {
                continue;
            }

            var position = 0;
            while (position < text.Length)
            {
                position = SkipWhitespace(text, position, text.Length);
                if (position >= text.Length)
                {
                    break;
                }

                var maxEnd = Math.Min(position + _maxChunkSize, text.Length);
                var end = maxEnd == text.Length
                    ? text.Length
                    : FindChunkEnd(text, position, maxEnd);

                if (end <= position)
                {
                    end = maxEnd;
                }

                var content = text[position..end].Trim();
                if (content.Length > 0)
                {
                    chunks.Add(new ChunkedText(
                        chunkIndex++,
                        content,
                        page.PageNumber,
                        page.SlideNumber));
                }

                if (end >= text.Length)
                {
                    break;
                }

                var nextPosition = FindOverlapStart(text, position, end);
                position = nextPosition > position ? nextPosition : end;
            }
        }

        return chunks;
    }

    private int FindChunkEnd(string text, int start, int maxEnd)
    {
        var minimumBoundary = start + Math.Max(_maxChunkSize / 3, 1);

        for (var index = maxEnd - 1; index >= minimumBoundary; index--)
        {
            if (text[index] == '\n'
                && index + 1 < text.Length
                && text[index + 1] == '\n')
            {
                return index;
            }
        }

        for (var index = maxEnd - 1; index >= minimumBoundary; index--)
        {
            if (IsSentenceEnd(text, index))
            {
                return index + 1;
            }
        }

        for (var index = maxEnd - 1; index >= minimumBoundary; index--)
        {
            if (text[index] == '\n')
            {
                return index;
            }
        }

        for (var index = maxEnd - 1; index >= minimumBoundary; index--)
        {
            if (char.IsWhiteSpace(text[index]))
            {
                return index;
            }
        }

        return maxEnd;
    }

    private int FindOverlapStart(string text, int chunkStart, int chunkEnd)
    {
        if (_overlapSize == 0)
        {
            return SkipWhitespace(text, chunkEnd, text.Length);
        }

        // Bounded overlap: search only within configured overlap window
        // Allow 1.5x configured overlap for finding natural boundaries
        var maxOverlapSearch = (int)(_overlapSize * 1.5);
        var minAllowedStart = Math.Max(chunkStart + 1, chunkEnd - maxOverlapSearch);

        // Desired start is at the end of the configured overlap window
        var desiredStart = Math.Max(minAllowedStart, chunkEnd - _overlapSize);

        // Search backwards from desiredStart for a natural boundary within the search window
        for (var index = desiredStart - 1; index >= minAllowedStart; index--)
        {
            if (IsSentenceEnd(text, index) || text[index] == '\n')
            {
                var sentenceStart = SkipWhitespace(text, index + 1, chunkEnd);
                if (sentenceStart > minAllowedStart && sentenceStart < chunkEnd)
                {
                    return sentenceStart;
                }
            }
        }

        // Fallback to word boundary at desiredStart
        var wordStart = desiredStart;
        if (wordStart > chunkStart && wordStart < text.Length)
        {
            // Skip to end of current word if not already at whitespace
            if (wordStart > 0 && !char.IsWhiteSpace(text[wordStart - 1]))
            {
                while (wordStart < text.Length && !char.IsWhiteSpace(text[wordStart]))
                {
                    wordStart++;
                }
            }
            wordStart = SkipWhitespace(text, wordStart, text.Length);
        }

        // Ensure deterministic forward progress: at least 1/4 of max chunk size
        var minProgress = Math.Max(_maxChunkSize / 4, 1);
        var absoluteMinStart = Math.Max(chunkStart + 1, chunkEnd - minProgress);

        if (wordStart < absoluteMinStart)
        {
            wordStart = absoluteMinStart;
        }

        // Cap at chunkEnd and ensure forward progress
        return wordStart < chunkEnd ? wordStart : absoluteMinStart;
    }

    private static bool IsSentenceEnd(string text, int index)
    {
        if (text[index] is not ('.' or '?' or '!'))
        {
            return false;
        }

        return index + 1 >= text.Length || char.IsWhiteSpace(text[index + 1]);
    }

    private static int SkipWhitespace(string text, int position, int limit)
    {
        while (position < limit && char.IsWhiteSpace(text[position]))
        {
            position++;
        }

        return position;
    }

    private static string NormalizeWhitespace(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = new System.Text.StringBuilder(text.Length);
        var pendingSpace = false;
        var pendingNewlines = 0;

        foreach (var character in text)
        {
            if (character == '\r')
            {
                continue;
            }

            if (character == '\n')
            {
                pendingSpace = false;
                pendingNewlines = Math.Min(pendingNewlines + 1, 2);
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                if (pendingNewlines == 0)
                {
                    pendingSpace = true;
                }

                continue;
            }

            if (normalized.Length > 0)
            {
                if (pendingNewlines > 0)
                {
                    normalized.Append('\n', pendingNewlines);
                }
                else if (pendingSpace)
                {
                    normalized.Append(' ');
                }
            }

            normalized.Append(character);
            pendingSpace = false;
            pendingNewlines = 0;
        }

        return normalized.ToString();
    }
}

public sealed record ChunkedText(int ChunkIndex, string Content, int? PageNumber, int? SlideNumber);
