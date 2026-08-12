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
            var text = page.Text?.Trim();

            if (!string.IsNullOrWhiteSpace(text))
            {
                pages.Add(new ParsedPage(text, page.Number, SlideNumber: null));
            }
        }

        return pages;
    }
}
