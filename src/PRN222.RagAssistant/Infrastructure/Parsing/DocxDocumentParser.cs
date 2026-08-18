using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace PRN222.RagAssistant.Infrastructure.Parsing;

public sealed class DocxDocumentParser : IDocumentParser
{
    public IReadOnlyList<ParsedPage> Parse(Stream fileStream)
    {
        var pages = new List<ParsedPage>();

        using var document = WordprocessingDocument.Open(fileStream, false);
        var body = document.MainDocumentPart?.Document?.Body;

        if (body is null)
        {
            return pages;
        }

        // Collect all non-empty paragraphs and treat the entire document as a single logical page
        // Blank paragraphs are spacing, not page breaks
        var allParagraphs = body.Descendants<Paragraph>().ToList();
        if (allParagraphs.Count == 0)
        {
            return pages;
        }

        var combinedText = new System.Text.StringBuilder();
        foreach (var paragraph in allParagraphs)
        {
            var text = paragraph.InnerText;
            if (string.IsNullOrWhiteSpace(text))
            {
                // Skip blank paragraphs - they are formatting/spacing, not page breaks
                continue;
            }

            if (combinedText.Length > 0)
            {
                combinedText.AppendLine();
                combinedText.AppendLine();
            }
            combinedText.Append(text.Trim());
        }

        var textContent = combinedText.ToString().Trim();
        if (!string.IsNullOrEmpty(textContent))
        {
            // Use null for PageNumber since DOCX doesn't provide reliable page metadata
            pages.Add(new ParsedPage(textContent, PageNumber: null, SlideNumber: null));
        }

        return pages;
    }
}
