namespace PRN222.RagAssistant.Infrastructure.Parsing;

public sealed class TextChunker
{
    private readonly int _maxChunkSize;
    private readonly int _overlapSize;

    public TextChunker(int maxChunkSize = 500, int overlapSize = 100)
    {
        _maxChunkSize = maxChunkSize;
        _overlapSize = overlapSize;
    }

    public IReadOnlyList<ChunkedText> Chunk(IReadOnlyList<ParsedPage> pages)
    {
        var chunks = new List<ChunkedText>();
        var chunkIndex = 0;

        foreach (var page in pages)
        {
            var text = page.Text;

            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (text.Length <= _maxChunkSize)
            {
                chunks.Add(new ChunkedText(chunkIndex++, text, page.PageNumber, page.SlideNumber));
                continue;
            }

            var position = 0;

            while (position < text.Length)
            {
                var remaining = text.Length - position;
                var length = Math.Min(_maxChunkSize, remaining);
                var segment = text.Substring(position, length);

                // Try to break at a sentence boundary if we're not at the end
                if (position + length < text.Length)
                {
                    var lastSentenceEnd = FindLastSentenceEnd(segment);

                    if (lastSentenceEnd > _maxChunkSize / 3)
                    {
                        segment = segment[..lastSentenceEnd].TrimEnd();
                        length = lastSentenceEnd;
                    }
                }

                chunks.Add(new ChunkedText(chunkIndex++, segment.Trim(), page.PageNumber, page.SlideNumber));

                // Move forward, applying overlap
                var advance = Math.Max(length - _overlapSize, 1);
                position += advance;
            }
        }

        return chunks;
    }

    private static int FindLastSentenceEnd(string text)
    {
        var lastPeriod = text.LastIndexOf(". ", StringComparison.Ordinal);
        var lastQuestion = text.LastIndexOf("? ", StringComparison.Ordinal);
        var lastExclamation = text.LastIndexOf("! ", StringComparison.Ordinal);
        var lastNewline = text.LastIndexOf('\n');

        var best = Math.Max(Math.Max(lastPeriod, lastQuestion), Math.Max(lastExclamation, lastNewline));

        return best > 0 ? best + 1 : -1;
    }
}

public sealed record ChunkedText(int ChunkIndex, string Content, int? PageNumber, int? SlideNumber);
