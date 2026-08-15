using System.Text;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig;

namespace PRN222.RagAssistant.Infrastructure.Parsing;

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
            .OrderByDescending(word => word.BoundingBox.BottomLeft.Y)
            .ThenBy(word => word.BoundingBox.BottomLeft.X)
            .ToList();

        if (words.Count == 0)
        {
            return page.Text?.Trim() ?? string.Empty;
        }

        var result = new StringBuilder();
        var currentLine = new List<Word>();
        var currentBaseline = words[0].BoundingBox.BottomLeft.Y;
        var currentHeight = words[0].BoundingBox.Height;

        foreach (var word in words)
        {
            var baseline = word.BoundingBox.BottomLeft.Y;
            var height = word.BoundingBox.Height;
            var lineTolerance = Math.Max(1.5, Math.Min(currentHeight, height) * 0.5);

            if (currentLine.Count > 0 && Math.Abs(baseline - currentBaseline) > lineTolerance)
            {
                AppendLine(result, currentLine);
                currentLine.Clear();
                currentBaseline = baseline;
                currentHeight = height;
            }

            currentLine.Add(word);
            currentBaseline = ((currentBaseline * (currentLine.Count - 1)) + baseline) / currentLine.Count;
            currentHeight = Math.Max(currentHeight, height);
        }

        AppendLine(result, currentLine);
        return result.ToString().Trim();
    }

    private static void AppendLine(StringBuilder result, List<Word> words)
    {
        if (words.Count == 0)
        {
            return;
        }

        words.Sort((left, right) =>
            left.BoundingBox.BottomLeft.X.CompareTo(right.BoundingBox.BottomLeft.X));

        if (result.Length > 0)
        {
            result.Append('\n');
        }

        string? previous = null;
        foreach (var word in words)
        {
            var text = word.Text.Trim();
            if (text.Length == 0)
            {
                continue;
            }

            if (previous is not null && NeedsSpace(previous, text))
            {
                result.Append(' ');
            }

            result.Append(text);
            previous = text;
        }
    }

    private static bool NeedsSpace(string previous, string current)
    {
        const string closingPunctuation = ",.;:!?%)]}";
        const string openingPunctuation = "([{";

        return !closingPunctuation.Contains(current[0])
               && !openingPunctuation.Contains(previous[^1]);
    }
}
