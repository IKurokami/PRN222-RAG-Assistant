using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

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
    private static readonly Regex BulletOrNumberRegex = new(@"^(\u2022|\u25E6|\u25AA|\u2013|-|\*|\d+[\.\)])\s+", RegexOptions.Compiled);

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

        var lines = GroupWordsIntoLines(words);
        if (lines.Count == 0)
        {
            return string.Empty;
        }

        var orderedLines = OrderLinesByLayout(lines, page.Width);
        return AssemblePageText(orderedLines);
    }

    private static List<PdfTextLine> GroupWordsIntoLines(IReadOnlyList<Word> words)
    {
        // Sort by baseline Y descending (top to bottom), then X ascending (left to right)
        var sortedWords = words
            .OrderByDescending(w => w.BoundingBox.BottomLeft.Y)
            .ThenBy(w => w.BoundingBox.BottomLeft.X)
            .ToList();

        var lines = new List<PdfTextLine>();
        var currentWords = new List<Word>();
        var currentBaseline = sortedWords[0].BoundingBox.BottomLeft.Y;
        var currentHeight = sortedWords[0].BoundingBox.Height;
        var tolerance = Math.Max(2.0, currentHeight * 0.45);

        foreach (var word in sortedWords)
        {
            var baseline = word.BoundingBox.BottomLeft.Y;
            var height = word.BoundingBox.Height;
            var prevWord = currentWords.LastOrDefault();

            var horizontalGap = prevWord is not null ? word.BoundingBox.Left - prevWord.BoundingBox.Right : 0;
            var isLargeHorizontalGap = prevWord is not null && horizontalGap > Math.Max(30.0, height * 2.5);

            if (currentWords.Count > 0 && (Math.Abs(baseline - currentBaseline) > tolerance || isLargeHorizontalGap))
            {
                lines.Add(CreateTextLine(currentWords));
                currentWords.Clear();
                currentBaseline = baseline;
                currentHeight = height;
                tolerance = Math.Max(2.0, currentHeight * 0.45);
            }

            currentWords.Add(word);
        }

        if (currentWords.Count > 0)
        {
            lines.Add(CreateTextLine(currentWords));
        }

        return lines;
    }


    private static PdfTextLine CreateTextLine(List<Word> words)
    {
        var sorted = words.OrderBy(w => w.BoundingBox.BottomLeft.X).ToList();
        var text = string.Join(" ", sorted.Select(w => w.Text.Trim()).Where(t => !string.IsNullOrEmpty(t)));
        var minX = sorted.Min(w => w.BoundingBox.BottomLeft.X);
        var maxX = sorted.Max(w => w.BoundingBox.BottomRight.X);
        var minY = sorted.Min(w => w.BoundingBox.BottomLeft.Y);
        var maxY = sorted.Max(w => w.BoundingBox.TopLeft.Y);
        var height = maxY - minY;

        return new PdfTextLine(text, minX, maxX, minY, maxY, height, sorted[0].BoundingBox.BottomLeft.Y);
    }

    private static List<PdfTextLine> OrderLinesByLayout(List<PdfTextLine> lines, double pageWidth)
    {
        if (lines.Count <= 3)
        {
            return lines.OrderByDescending(l => l.BaselineY).ToList();
        }

        // Check for 2-column layout
        var minX = lines.Min(l => l.MinX);
        var maxX = lines.Max(l => l.MaxX);
        var contentWidth = maxX - minX;

        if (contentWidth > 150)
        {
            var midX = minX + contentWidth / 2.0;

            // Lines that clearly sit on left or right
            var leftLines = lines.Where(l => l.MaxX <= midX + 20 && l.Width < contentWidth * 0.7).ToList();
            var rightLines = lines.Where(l => l.MinX >= midX - 20 && l.Width < contentWidth * 0.7).ToList();

            // Multi-column confirmed if both left and right have multiple lines
            if (leftLines.Count >= 3 && rightLines.Count >= 3)
            {
                var fullWidthThreshold = contentWidth * 0.75;
                var headerLines = new List<PdfTextLine>();
                var footerLines = new List<PdfTextLine>();
                var colLeftLines = new List<PdfTextLine>();
                var colRightLines = new List<PdfTextLine>();

                var colTopY = Math.Max(
                    leftLines.Max(l => l.MaxY),
                    rightLines.Max(l => l.MaxY));
                var colBottomY = Math.Min(
                    leftLines.Min(l => l.MinY),
                    rightLines.Min(l => l.MinY));

                foreach (var line in lines)
                {
                    if (line.Width >= fullWidthThreshold && line.MinY > colTopY - 10)
                    {
                        headerLines.Add(line);
                    }
                    else if (line.Width >= fullWidthThreshold && line.MaxY < colBottomY + 10)
                    {
                        footerLines.Add(line);
                    }
                    else if (line.MaxX <= midX + 25)
                    {
                        colLeftLines.Add(line);
                    }
                    else if (line.MinX >= midX - 25)
                    {
                        colRightLines.Add(line);
                    }
                    else
                    {
                        // Cross-spanning line in middle: place based on vertical position
                        if (line.MinY > (colTopY + colBottomY) / 2)
                        {
                            headerLines.Add(line);
                        }
                        else
                        {
                            colLeftLines.Add(line);
                        }
                    }
                }

                var result = new List<PdfTextLine>();
                result.AddRange(headerLines.OrderByDescending(l => l.BaselineY));
                result.AddRange(colLeftLines.OrderByDescending(l => l.BaselineY));
                result.AddRange(colRightLines.OrderByDescending(l => l.BaselineY));
                result.AddRange(footerLines.OrderByDescending(l => l.BaselineY));
                return result;
            }
        }

        // Single-column: top to bottom
        return lines.OrderByDescending(l => l.BaselineY).ToList();
    }

    private static string AssemblePageText(IReadOnlyList<PdfTextLine> lines)
    {
        if (lines.Count == 0)
        {
            return string.Empty;
        }

        // Calculate median vertical line spacing between consecutive lines
        var lineGaps = new List<double>();
        for (var i = 1; i < lines.Count; i++)
        {
            var gap = lines[i - 1].MinY - lines[i].MaxY;
            if (gap >= 0 && gap < lines[i - 1].Height * 4)
            {
                lineGaps.Add(gap);
            }
        }

        var medianGap = lineGaps.Count > 0 ? lineGaps.Median() : 4.0;
        var paragraphGapThreshold = Math.Max(6.0, medianGap * 1.4);

        var sb = new StringBuilder();
        sb.Append(lines[0].Text);

        for (var i = 1; i < lines.Count; i++)
        {
            var prevLine = lines[i - 1];
            var currLine = lines[i];
            var gap = prevLine.MinY - currLine.MaxY;

            var isParagraphBreak =
                gap > paragraphGapThreshold
                || BulletOrNumberRegex.IsMatch(currLine.Text)
                || IsHeadingLine(prevLine, currLine, medianGap);

            if (isParagraphBreak)
            {
                sb.Append("\n\n");
            }
            else
            {
                // Soft wrap: join with space if prev line doesn't end with hyphen
                if (prevLine.Text.EndsWith('-') && prevLine.Text.Length > 1 && char.IsLetter(prevLine.Text[^2]))
                {
                    // De-hyphenate: remove hyphen and append directly
                    sb.Remove(sb.Length - 1, 1);
                }
                else
                {
                    sb.Append(' ');
                }
            }

            sb.Append(currLine.Text);
        }

        return sb.ToString().Trim();
    }

    private static bool IsHeadingLine(PdfTextLine prev, PdfTextLine curr, double medianGap)
    {
        // If previous line is significantly taller (larger font), it's likely a heading
        if (prev.Height > curr.Height * 1.25 && prev.Text.Length < 100)
        {
            return true;
        }

        // If previous line is very short and doesn't end with punctuation, might be heading
        if (prev.Text.Length < 60 && !prev.Text.EndsWith('.') && !prev.Text.EndsWith(',') && !prev.Text.EndsWith(';') && !prev.Text.EndsWith(':'))
        {
            var gap = prev.MinY - curr.MaxY;
            if (gap > medianGap * 1.1)
            {
                return true;
            }
        }

        return false;
    }

    private sealed record PdfTextLine(
        string Text,
        double MinX,
        double MaxX,
        double MinY,
        double MaxY,
        double Height,
        double BaselineY)
    {
        public double Width => MaxX - MinX;
    }
}

