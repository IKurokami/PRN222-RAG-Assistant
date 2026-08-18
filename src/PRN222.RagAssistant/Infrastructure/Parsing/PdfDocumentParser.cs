using System.Text;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig;

namespace PRN222.RagAssistant.Infrastructure.Parsing;

public static class EnumerableExtensions
{
    public static double Median(this IEnumerable<double> source)
    {
        var sortedList = source.OrderBy(x => x).ToList();
        var count = sortedList.Count;
        if (count == 0)
        {
            return 0;
        }
        var midIndex = count / 2;
        return count % 2 == 0
            ? (sortedList[midIndex - 1] + sortedList[midIndex]) / 2
            : sortedList[midIndex];
    }
}

public sealed class PdfDocumentParser : IDocumentParser
{
    public IReadOnlyList<ParsedPage> Parse(Stream fileStream)
    {
        var pages = new List<ParsedPage>();

        using var document = PdfDocument.Open(fileStream);

        foreach (var page in document.GetPages())
        {
            var text = ExtractText(page);

            if (!string.IsNullOrWhiteSpace(text))
            {
                pages.Add(new ParsedPage(text, page.Number, SlideNumber: null));
            }
        }

        return pages;
    }

    private static string ExtractText(Page page)
    {
        var words = page.GetWords()
            .Where(word => !string.IsNullOrWhiteSpace(word.Text))
            .ToList();

        if (words.Count == 0)
        {
            return page.Text?.Trim() ?? string.Empty;
        }

        // Group words into columns and lines using layout-aware algorithm
        var pageWidth = page.Width;
        var orderedLines = ExtractLinesWithReadingOrder(words, pageWidth);

        var result = new StringBuilder();
        for (var i = 0; i < orderedLines.Count; i++)
        {
            if (i > 0)
            {
                result.Append('\n');
            }
            result.Append(string.Join(" ", orderedLines[i]));
        }

        return result.ToString().Trim();
    }

    private static List<List<string>> ExtractLinesWithReadingOrder(IReadOnlyList<Word> words, double pageWidth)
    {
        if (words.Count == 0)
        {
            return new List<List<string>>();
        }

        // Detect if this is likely a multi-column layout by analyzing X positions
        var xPositions = words.Select(w => w.BoundingBox.BottomLeft.X).OrderBy(x => x).ToList();
        var medianX = xPositions[xPositions.Count / 2];
        var isMultiColumn = DetectMultiColumnLayout(words, pageWidth);

        if (isMultiColumn)
        {
            return ExtractMultiColumnLines(words, pageWidth);
        }

        // Single column: use improved Y-then-X sorting with better line grouping
        return ExtractSingleColumnLines(words);
    }

    private static bool DetectMultiColumnLayout(IReadOnlyList<Word> words, double pageWidth)
    {
        if (words.Count < 10)
        {
            return false;
        }

        // Analyze X positions to detect column separation
        // If words cluster into distinct X regions, it's likely multi-column
        var xPositions = words
            .Select(w => w.BoundingBox.BottomLeft.X)
            .OrderBy(x => x)
            .ToList();

        // Calculate gaps in X positions
        var gaps = new List<double>();
        for (var i = 1; i < xPositions.Count; i++)
        {
            var gap = xPositions[i] - xPositions[i - 1];
            if (gap > 50) // Significant gap indicates column boundary
            {
                gaps.Add(gap);
            }
        }

        // If we have multiple significant gaps, it's likely multi-column
        return gaps.Count >= 2;
    }

    private static List<List<string>> ExtractSingleColumnLines(IReadOnlyList<Word> words)
    {
        // Sort by Y (descending for PDF coordinates where Y=0 is bottom)
        // then by X (ascending for left-to-right)
        var sortedWords = words
            .OrderByDescending(w => w.BoundingBox.BottomLeft.Y)
            .ThenBy(w => w.BoundingBox.BottomLeft.X)
            .ToList();

        return GroupWordsIntoLines(sortedWords);
    }

    private static List<List<string>> ExtractMultiColumnLines(IReadOnlyList<Word> words, double pageWidth)
    {
        // Find column boundaries by analyzing X position clusters
        var columnBoundaries = FindColumnBoundaries(words, pageWidth);

        // Sort words by Y then X (same as single column)
        var sortedWords = words
            .OrderByDescending(w => w.BoundingBox.BottomLeft.Y)
            .ThenBy(w => w.BoundingBox.BottomLeft.X)
            .ToList();

        // Group into lines with column awareness
        var lines = new List<List<string>>();
        var currentLine = new List<Word>();
        var currentBaseline = sortedWords.Count > 0 ? sortedWords[0].BoundingBox.BottomLeft.Y : 0;
        var currentHeight = sortedWords.Count > 0 ? sortedWords[0].BoundingBox.Height : 0;
        var lineTolerance = Math.Max(2.0, currentHeight * 0.5);

        foreach (var word in sortedWords)
        {
            var baseline = word.BoundingBox.BottomLeft.Y;
            var height = word.BoundingBox.Height;

            if (currentLine.Count > 0 && Math.Abs(baseline - currentBaseline) > lineTolerance)
            {
                // New line detected - sort by X and add
                var sortedLine = currentLine
                    .OrderBy(w => GetColumnIndex(w, columnBoundaries))
                    .ThenBy(w => w.BoundingBox.BottomLeft.X)
                    .Select(w => w.Text.Trim())
                    .Where(t => !string.IsNullOrEmpty(t))
                    .ToList();

                if (sortedLine.Count > 0)
                {
                    lines.Add(sortedLine);
                }

                currentLine.Clear();
                currentBaseline = baseline;
                currentHeight = height;
                lineTolerance = Math.Max(2.0, currentHeight * 0.5);
            }

            currentLine.Add(word);
        }

        // Add remaining line
        if (currentLine.Count > 0)
        {
            var sortedLine = currentLine
                .OrderBy(w => GetColumnIndex(w, columnBoundaries))
                .ThenBy(w => w.BoundingBox.BottomLeft.X)
                .Select(w => w.Text.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .ToList();

            if (sortedLine.Count > 0)
            {
                lines.Add(sortedLine);
            }
        }

        return lines;
    }

    private static List<double> FindColumnBoundaries(IReadOnlyList<Word> words, double pageWidth)
    {
        // Simple column detection: divide page into columns based on word distribution
        var boundaries = new List<double>();

        // Group words by rough X position
        var xBuckets = new Dictionary<int, List<double>>();
        foreach (var word in words)
        {
            var bucket = (int)(word.BoundingBox.BottomLeft.X / (pageWidth / 10));
            if (!xBuckets.ContainsKey(bucket))
            {
                xBuckets[bucket] = new List<double>();
            }
            xBuckets[bucket].Add(word.BoundingBox.BottomLeft.X);
        }

        // Find gaps between buckets
        var sortedBuckets = xBuckets.Keys.OrderBy(k => k).ToList();
        for (var i = 1; i < sortedBuckets.Count; i++)
        {
            var currentBucketMedian = xBuckets[sortedBuckets[i - 1]].Median();
            var nextBucketMedian = xBuckets[sortedBuckets[i]].Median();

            if (nextBucketMedian - currentBucketMedian > pageWidth / 6)
            {
                // Significant gap - this is likely a column boundary
                boundaries.Add((currentBucketMedian + nextBucketMedian) / 2);
            }
        }

        return boundaries;
    }

    private static int GetColumnIndex(Word word, List<double> boundaries)
    {
        var x = word.BoundingBox.BottomLeft.X;
        for (var i = 0; i < boundaries.Count; i++)
        {
            if (x < boundaries[i])
            {
                return i;
            }
        }
        return boundaries.Count;
    }

    private static List<List<string>> GroupWordsIntoLines(IReadOnlyList<Word> sortedWords)
    {
        var lines = new List<List<string>>();
        if (sortedWords.Count == 0)
        {
            return lines;
        }

        var currentLine = new List<Word>();
        var currentBaseline = sortedWords[0].BoundingBox.BottomLeft.Y;
        var currentHeight = sortedWords[0].BoundingBox.Height;
        var lineTolerance = Math.Max(2.0, currentHeight * 0.5);

        foreach (var word in sortedWords)
        {
            var baseline = word.BoundingBox.BottomLeft.Y;
            var height = word.BoundingBox.Height;

            if (currentLine.Count > 0 && Math.Abs(baseline - currentBaseline) > lineTolerance)
            {
                // New line
                var sortedLine = currentLine
                    .OrderBy(w => w.BoundingBox.BottomLeft.X)
                    .Select(w => w.Text.Trim())
                    .Where(t => !string.IsNullOrEmpty(t))
                    .ToList();

                if (sortedLine.Count > 0)
                {
                    lines.Add(sortedLine);
                }

                currentLine.Clear();
                currentBaseline = baseline;
                currentHeight = height;
                lineTolerance = Math.Max(2.0, currentHeight * 0.5);
            }

            currentLine.Add(word);
        }

        // Add final line
        if (currentLine.Count > 0)
        {
            var sortedLine = currentLine
                .OrderBy(w => w.BoundingBox.BottomLeft.X)
                .Select(w => w.Text.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .ToList();

            if (sortedLine.Count > 0)
            {
                lines.Add(sortedLine);
            }
        }

        return lines;
    }
}
