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

        var paragraphs = body.Elements<Paragraph>().ToList();
        var currentBlock = new System.Text.StringBuilder();
        var blockIndex = 1;

        foreach (var paragraph in paragraphs)
        {
            var text = paragraph.InnerText?.Trim();

            if (string.IsNullOrWhiteSpace(text))
            {
                if (currentBlock.Length > 0)
                {
                    pages.Add(new ParsedPage(currentBlock.ToString().Trim(), PageNumber: blockIndex, SlideNumber: null));
                    currentBlock.Clear();
                    blockIndex++;
                }
                continue;
            }

            currentBlock.AppendLine(text);
        }

        if (currentBlock.Length > 0)
        {
            pages.Add(new ParsedPage(currentBlock.ToString().Trim(), PageNumber: blockIndex, SlideNumber: null));
        }

        return pages;
    }
}
