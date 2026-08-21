using System.Text;
using DocumentFormat.OpenXml;
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

        var elements = ExtractBodyElements(body);
        if (elements.Count == 0)
        {
            return pages;
        }

        var textContent = string.Join("\n\n", elements).Trim();
        if (!string.IsNullOrEmpty(textContent))
        {
            // Use null for PageNumber since DOCX doesn't provide reliable page metadata
            pages.Add(new ParsedPage(textContent, PageNumber: null, SlideNumber: null));
        }

        return pages;
    }

    private static List<string> ExtractBodyElements(Body body)
    {
        var blocks = new List<string>();

        foreach (var element in body.Elements())
        {
            if (element is Paragraph paragraph)
            {
                var text = ExtractParagraphText(paragraph);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    blocks.Add(text);
                }
            }
            else if (element is Table table)
            {
                var tableText = ExtractTableText(table);
                if (!string.IsNullOrWhiteSpace(tableText))
                {
                    blocks.Add(tableText);
                }
            }
        }

        return blocks;
    }

    private static string ExtractParagraphText(Paragraph paragraph)
    {
        var text = paragraph.InnerText?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var isListItem = paragraph.ParagraphProperties?.NumberingProperties is not null;
        if (isListItem && !text.StartsWith("- ") && !text.StartsWith("* ") && !char.IsDigit(text[0]))
        {
            return $"- {text}";
        }

        return text;
    }

    private static string ExtractTableText(Table table)
    {
        var rows = new List<string>();

        foreach (var row in table.Elements<TableRow>())
        {
            var cells = new List<string>();
            foreach (var cell in row.Elements<TableCell>())
            {
                var cellParagraphs = cell.Descendants<Paragraph>()
                    .Select(p => p.InnerText?.Trim())
                    .Where(t => !string.IsNullOrWhiteSpace(t));
                var cellContent = string.Join(" ", cellParagraphs).Trim();
                cells.Add(cellContent);
            }

            if (cells.Any(c => !string.IsNullOrEmpty(c)))
            {
                rows.Add($"| {string.Join(" | ", cells)} |");
            }
        }

        return string.Join("\n", rows);
    }
}

