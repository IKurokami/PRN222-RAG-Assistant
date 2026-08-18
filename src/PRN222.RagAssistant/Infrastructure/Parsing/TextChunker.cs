using System.Globalization;
using System.Text;
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
    public static TextChunker Create(int maxChunkSize = 1000, int overlapSize = 0)
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
        var validPages = pages
            .Where(p => !string.IsNullOrWhiteSpace(p.Text))
            .ToList();

        if (validPages.Count == 0)
        {
            return Array.Empty<ChunkedText>();
        }

        // Build continuous document text with character-to-page mapping
        var combinedText = new StringBuilder();
        var pageSpans = new List<PageSpan>();

        for (var i = 0; i < validPages.Count; i++)
        {
            var page = validPages[i];
            var pageText = NormalizeWhitespace(page.Text);
            if (pageText.Length == 0)
            {
                continue;
            }

            if (combinedText.Length > 0)
            {
                var prevPageText = validPages[i - 1].Text.Trim();
                var endsWithPunctuation = prevPageText.EndsWith('.') || prevPageText.EndsWith('!') || prevPageText.EndsWith('?') || prevPageText.EndsWith(':') || prevPageText.EndsWith(';') || prevPageText.EndsWith('"') || prevPageText.EndsWith('”');
                var isShortTitlePage = !prevPageText.Contains('\n') && prevPageText.Length < 60;

                if (endsWithPunctuation || isShortTitlePage)
                {
                    combinedText.Append("\n\n");
                }
                else if (prevPageText.EndsWith('-') && prevPageText.Length > 1 && char.IsLetter(prevPageText[^2]))
                {
                    // De-hyphenate across page break
                    combinedText.Remove(combinedText.Length - 1, 1);
                }
                else
                {
                    combinedText.Append(' ');
                }
            }


            var start = combinedText.Length;
            combinedText.Append(pageText);
            var end = combinedText.Length;
            pageSpans.Add(new PageSpan(start, end, page.PageNumber, page.SlideNumber));
        }

        var fullText = combinedText.ToString();
        if (fullText.Length == 0)
        {
            return Array.Empty<ChunkedText>();
        }

        var chunks = new List<ChunkedText>();
        var chunkIndex = 0;
        var position = 0;

        while (position < fullText.Length)
        {
            position = SkipWhitespace(fullText, position, fullText.Length);
            if (position >= fullText.Length)
            {
                break;
            }

            var maxEnd = Math.Min(position + _maxChunkSize, fullText.Length);
            maxEnd = AdjustToGraphemeBoundary(fullText, maxEnd);

            var end = maxEnd == fullText.Length
                ? fullText.Length
                : FindChunkEnd(fullText, position, maxEnd);

            if (end <= position)
            {
                end = maxEnd;
            }

            end = AdjustToGraphemeBoundary(fullText, end);

            var content = fullText[position..end].Trim();
            if (content.Length > 0)
            {
                var midPos = (position + end) / 2;
                var span = pageSpans.FirstOrDefault(s => midPos >= s.Start && midPos < s.End)
                           ?? pageSpans.FirstOrDefault(s => position >= s.Start && position <= s.End)
                           ?? pageSpans.LastOrDefault();

                chunks.Add(new ChunkedText(
                    chunkIndex++,
                    content,
                    span?.PageNumber,
                    span?.SlideNumber));
            }

            if (end >= fullText.Length)
            {
                break;
            }

            var nextPosition = FindOverlapStart(fullText, position, end);
            position = nextPosition > position ? nextPosition : end;
        }

        return chunks;
    }

    private sealed record PageSpan(int Start, int End, int? PageNumber, int? SlideNumber);


    private int FindChunkEnd(string text, int start, int maxEnd)
    {
        var minimumBoundary = start + Math.Max(_maxChunkSize / 3, 1);

        // Priority 1: Double newline (paragraph break)
        for (var index = maxEnd - 1; index >= minimumBoundary; index--)
        {
            if (text[index] == '\n'
                && index + 1 < text.Length
                && text[index + 1] == '\n')
            {
                return index;
            }
        }

        // Priority 2: Sentence end (. ! ?)
        for (var index = maxEnd - 1; index >= minimumBoundary; index--)
        {
            if (IsSentenceEnd(text, index))
            {
                return index + 1;
            }
        }

        // Priority 3: Semicolon / Colon / List item boundary
        for (var index = maxEnd - 1; index >= minimumBoundary; index--)
        {
            if ((text[index] is ';' or ':') && index + 1 < text.Length && char.IsWhiteSpace(text[index + 1]))
            {
                return index + 1;
            }

            if (text[index] == '\n' && index + 1 < text.Length && (text[index + 1] is '-' or '*' or '\u2022' || char.IsDigit(text[index + 1])))
            {
                return index;
            }
        }

        // Priority 4: Word boundary (whitespace)
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

        // Absolute minimum start to guarantee forward progress (at least 25% of max chunk size from chunkStart)
        var minProgress = Math.Max(_maxChunkSize / 4, 1);
        var absoluteMinStart = Math.Max(chunkStart + 1, Math.Min(chunkStart + minProgress, chunkEnd));

        // Bounded overlap search window: search up to 1.5x configured overlap
        var maxOverlapSearch = (int)(_overlapSize * 1.5);
        var minAllowedStart = Math.Max(absoluteMinStart, chunkEnd - maxOverlapSearch);

        // Desired start point based on configured overlap
        var desiredStart = Math.Max(minAllowedStart, chunkEnd - _overlapSize);

        // 1. Search for paragraph break (\n\n)
        for (var index = desiredStart - 1; index >= minAllowedStart; index--)
        {
            if (text[index] == '\n' && index + 1 < text.Length && text[index + 1] == '\n')
            {
                var sentenceStart = SkipWhitespace(text, index + 2, chunkEnd);
                if (sentenceStart >= minAllowedStart && sentenceStart < chunkEnd)
                {
                    return AdjustToGraphemeBoundary(text, sentenceStart);
                }
            }
        }

        // 2. Search for sentence end (. ! ?)
        for (var index = desiredStart - 1; index >= minAllowedStart; index--)
        {
            if (IsSentenceEnd(text, index))
            {
                var sentenceStart = SkipWhitespace(text, index + 1, chunkEnd);
                if (sentenceStart >= minAllowedStart && sentenceStart < chunkEnd)
                {
                    return AdjustToGraphemeBoundary(text, sentenceStart);
                }
            }
        }

        // 3. Search for list item / semicolon boundary
        for (var index = desiredStart - 1; index >= minAllowedStart; index--)
        {
            if ((text[index] is ';' or ':') && index + 1 < text.Length && char.IsWhiteSpace(text[index + 1]))
            {
                var boundaryStart = SkipWhitespace(text, index + 1, chunkEnd);
                if (boundaryStart >= minAllowedStart && boundaryStart < chunkEnd)
                {
                    return AdjustToGraphemeBoundary(text, boundaryStart);
                }
            }
        }

        // 4. Fallback to word boundary near desiredStart
        var wordStart = desiredStart;
        if (wordStart > chunkStart && wordStart < text.Length)
        {
            var scan = wordStart;
            while (scan < chunkEnd && scan - wordStart <= 20 && !char.IsWhiteSpace(text[scan]))
            {
                scan++;
            }
            if (scan < chunkEnd && char.IsWhiteSpace(text[scan]))
            {
                wordStart = SkipWhitespace(text, scan, text.Length);
            }
        }

        wordStart = AdjustToGraphemeBoundary(text, wordStart);

        if (wordStart < minAllowedStart)
        {
            wordStart = minAllowedStart;
        }

        return wordStart < chunkEnd ? wordStart : minAllowedStart;
    }



    private static bool IsSentenceEnd(string text, int index)
    {
        if (text[index] is not ('.' or '?' or '!'))
        {
            return false;
        }

        // Avoid splitting on decimal numbers (e.g. 1.5, 3.14) or abbreviations if next char is digit
        if (index + 1 < text.Length && char.IsDigit(text[index + 1]))
        {
            return false;
        }

        return index + 1 >= text.Length || char.IsWhiteSpace(text[index + 1]);
    }

    private static int AdjustToGraphemeBoundary(string text, int index)
    {
        if (index <= 0 || index >= text.Length)
        {
            return index;
        }

        // Avoid splitting UTF-16 surrogate pairs
        if (char.IsLowSurrogate(text[index]))
        {
            return index - 1;
        }

        // Avoid splitting combining character sequences (NonSpacingMark)
        if (char.GetUnicodeCategory(text[index]) == UnicodeCategory.NonSpacingMark)
        {
            var adjusted = index;
            while (adjusted > 0 && char.GetUnicodeCategory(text[adjusted]) == UnicodeCategory.NonSpacingMark)
            {
                adjusted--;
            }
            return adjusted;
        }

        return index;
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

        // Normalize Unicode canonical decomposition/composition to Form C
        text = text.Normalize(NormalizationForm.FormC);

        var normalized = new StringBuilder(text.Length);
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

