namespace PRN222.RagAssistant.Infrastructure.Parsing;

public sealed class TextChunker
{
    private readonly int _maxChunkSize;
    private readonly int _overlapSize;

    public TextChunker(int maxChunkSize = 500, int overlapSize = 100)
    {
        if (maxChunkSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxChunkSize));
        }

        if (overlapSize < 0 || overlapSize >= maxChunkSize)
        {
            throw new ArgumentOutOfRangeException(nameof(overlapSize));
        }

        _maxChunkSize = maxChunkSize;
        _overlapSize = overlapSize;
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

        var desiredStart = Math.Max(chunkStart + 1, chunkEnd - _overlapSize);

        for (var index = desiredStart - 1; index > chunkStart; index--)
        {
            if (IsSentenceEnd(text, index) || text[index] == '\n')
            {
                var sentenceStart = SkipWhitespace(text, index + 1, chunkEnd);
                if (sentenceStart > chunkStart && sentenceStart < chunkEnd)
                {
                    return sentenceStart;
                }
            }
        }

        var wordStart = desiredStart;
        if (wordStart > chunkStart
            && wordStart < chunkEnd
            && !char.IsWhiteSpace(text[wordStart - 1])
            && !char.IsWhiteSpace(text[wordStart]))
        {
            while (wordStart < chunkEnd && !char.IsWhiteSpace(text[wordStart]))
            {
                wordStart++;
            }
        }

        wordStart = SkipWhitespace(text, wordStart, chunkEnd);
        return wordStart < chunkEnd ? wordStart : desiredStart;
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
