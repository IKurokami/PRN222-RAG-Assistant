using DocumentFormat.OpenXml;
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
        if (slidePart.Slide is null)
        {
            return string.Empty;
        }

        // Extract text from shapes respecting their order in the slide
        // Group paragraphs by their containing shape and process shapes in document order
        var shapeTexts = ExtractShapeTextsInOrder(slidePart);

        if (shapeTexts.Count == 0)
        {
            return string.Empty;
        }

        return string.Join("\n\n", shapeTexts);
    }

    private static List<string> ExtractShapeTextsInOrder(SlidePart slidePart)
    {
        var texts = new List<string>();

        // Get all elements in document order
        var allElements = slidePart.Slide.Elements().ToList();

        foreach (var element in allElements)
        {
            var elementText = ExtractTextFromElement(element);
            if (!string.IsNullOrWhiteSpace(elementText))
            {
                texts.Add(elementText);
            }
        }

        return texts;
    }

    private static string ExtractTextFromElement(OpenXmlElement element)
    {
        var sb = new System.Text.StringBuilder();
        var paragraphText = new System.Text.StringBuilder();

        foreach (var descendant in element.Descendants<DocumentFormat.OpenXml.Drawing.Paragraph>())
        {
            var text = descendant.InnerText?.Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                if (paragraphText.Length > 0)
                {
                    paragraphText.Append(' ');
                }
                paragraphText.Append(text);
            }
        }

        // Also try legacy paragraph types
        foreach (var paragraph in element.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
        {
            var text = paragraph.InnerText?.Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                if (paragraphText.Length > 0)
                {
                    paragraphText.Append(' ');
                }
                paragraphText.Append(text);
            }
        }

        return paragraphText.ToString().Trim();
    }
}
