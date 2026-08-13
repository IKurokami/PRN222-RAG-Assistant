using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;

namespace PRN222.RagAssistant.Infrastructure.Parsing;

public sealed class PptxDocumentParser : IDocumentParser
{
    public IReadOnlyList<ParsedPage> Parse(Stream fileStream)
    {
        var pages = new List<ParsedPage>();

        using var document = PresentationDocument.Open(fileStream, false);
        var presentationPart = document.PresentationPart;

        if (presentationPart?.Presentation?.SlideIdList is null)
        {
            return pages;
        }

        var slideIds = presentationPart.Presentation.SlideIdList.Elements<SlideId>().ToList();

        for (var i = 0; i < slideIds.Count; i++)
        {
            var slideId = slideIds[i];
            var relationshipId = slideId.RelationshipId;

            if (relationshipId is null)
            {
                continue;
            }

            var slidePart = (SlidePart)presentationPart.GetPartById(relationshipId!);
            var slideText = ExtractSlideText(slidePart);

            if (!string.IsNullOrWhiteSpace(slideText))
            {
                pages.Add(new ParsedPage(slideText.Trim(), PageNumber: null, SlideNumber: i + 1));
            }
        }

        return pages;
    }

    private static string ExtractSlideText(SlidePart slidePart)
    {
        var sb = new System.Text.StringBuilder();

        if (slidePart.Slide is null)
        {
            return string.Empty;
        }

        foreach (var paragraph in slidePart.Slide.Descendants<DocumentFormat.OpenXml.Drawing.Paragraph>())
        {
            var text = paragraph.InnerText?.Trim();

            if (!string.IsNullOrWhiteSpace(text))
            {
                sb.AppendLine(text);
            }
        }

        return sb.ToString();
    }
}
