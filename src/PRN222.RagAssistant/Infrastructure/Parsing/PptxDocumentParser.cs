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

        if (slidePart.Slide?.CommonSlideData?.ShapeTree is null)
        {
            return string.Empty;
        }

        foreach (var shape in slidePart.Slide.CommonSlideData.ShapeTree.Elements<Shape>())
        {
            var textBody = shape.TextBody;

            if (textBody is null)
            {
                continue;
            }

            foreach (var paragraph in textBody.Elements<DocumentFormat.OpenXml.Drawing.Paragraph>())
            {
                var text = paragraph.InnerText?.Trim();

                if (!string.IsNullOrWhiteSpace(text))
                {
                    sb.AppendLine(text);
                }
            }
        }

        return sb.ToString();
    }
}
