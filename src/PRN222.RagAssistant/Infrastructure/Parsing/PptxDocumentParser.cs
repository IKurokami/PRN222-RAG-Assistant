using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using DParagraph = DocumentFormat.OpenXml.Drawing.Paragraph;
using PShape = DocumentFormat.OpenXml.Presentation.Shape;
using PGraphicFrame = DocumentFormat.OpenXml.Presentation.GraphicFrame;
using PGroupShape = DocumentFormat.OpenXml.Presentation.GroupShape;

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
        var slide = slidePart.Slide;
        if (slide is null)
        {
            return string.Empty;
        }

        var shapeTree = slide.CommonSlideData?.ShapeTree;
        if (shapeTree is null)
        {
            return string.Empty;
        }

        var positionedBlocks = new List<PositionedBlock>();
        var fallbackOrder = 0;

        foreach (var element in shapeTree.Elements())
        {
            ExtractPositionedBlocks(element, positionedBlocks, ref fallbackOrder);
        }

        if (positionedBlocks.Count == 0)
        {
            return string.Empty;
        }

        // Sort spatially: Top to bottom (Y ascending), then Left to right (X ascending)
        // Group shapes on approximately the same Y level (within vertical tolerance)
        var sorted = positionedBlocks
            .OrderBy(b => b.Y)
            .ThenBy(b => b.X)
            .ThenBy(b => b.OrderIndex)
            .Select(b => b.Text)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();

        return string.Join("\n\n", sorted);
    }

    private static void ExtractPositionedBlocks(
        OpenXmlElement element,
        List<PositionedBlock> result,
        ref int fallbackOrder)
    {
        if (element is PShape shape)
        {
            var (x, y) = GetShapeCoordinates(shape);
            var text = ExtractTextFromShape(shape);
            if (!string.IsNullOrWhiteSpace(text))
            {
                result.Add(new PositionedBlock(text, x, y, fallbackOrder++));
            }
        }
        else if (element is PGraphicFrame frame)
        {
            var (x, y) = GetFrameCoordinates(frame);
            var table = frame.Descendants<Table>().FirstOrDefault();
            if (table is not null)
            {
                var tableText = ExtractTextFromTable(table);
                if (!string.IsNullOrWhiteSpace(tableText))
                {
                    result.Add(new PositionedBlock(tableText, x, y, fallbackOrder++));
                }
            }
            else
            {
                var text = ExtractTextFromDescendantParagraphs(frame);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    result.Add(new PositionedBlock(text, x, y, fallbackOrder++));
                }
            }
        }
        else if (element is PGroupShape groupShape)
        {
            foreach (var child in groupShape.Elements())
            {
                ExtractPositionedBlocks(child, result, ref fallbackOrder);
            }
        }
    }

    private static (long X, long Y) GetShapeCoordinates(PShape shape)
    {
        var xfrm = shape.ShapeProperties?.Transform2D;
        if (xfrm?.Offset is not null)
        {
            return (xfrm.Offset.X?.Value ?? 0, xfrm.Offset.Y?.Value ?? 0);
        }
        return (0, 0);
    }

    private static (long X, long Y) GetFrameCoordinates(PGraphicFrame frame)
    {
        var xfrm = frame.Transform;
        if (xfrm?.Offset is not null)
        {
            return (xfrm.Offset.X?.Value ?? 0, xfrm.Offset.Y?.Value ?? 0);
        }
        return (0, 0);
    }

    private static string ExtractTextFromShape(PShape shape)
    {
        var paragraphs = shape.TextBody?.Elements<DParagraph>()
            .Select(ExtractParagraphText)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();

        if (paragraphs is null || paragraphs.Count == 0)
        {
            return string.Empty;
        }

        return string.Join("\n", paragraphs);
    }

    private static string ExtractParagraphText(DParagraph paragraph)
    {
        var runs = paragraph.Descendants<DocumentFormat.OpenXml.Drawing.Text>()
            .Select(t => t.Text)
            .Where(t => !string.IsNullOrWhiteSpace(t));

        return string.Join("", runs).Trim();
    }

    private static string ExtractTextFromTable(Table table)
    {
        var rows = new List<string>();
        foreach (var row in table.Elements<TableRow>())
        {
            var cells = new List<string>();
            foreach (var cell in row.Elements<TableCell>())
            {
                var cellParagraphs = cell.Descendants<DParagraph>()
                    .Select(ExtractParagraphText)
                    .Where(t => !string.IsNullOrWhiteSpace(t));
                cells.Add(string.Join(" ", cellParagraphs).Trim());
            }

            if (cells.Any(c => !string.IsNullOrEmpty(c)))
            {
                rows.Add($"| {string.Join(" | ", cells)} |");
            }
        }

        return string.Join("\n", rows);
    }

    private static string ExtractTextFromDescendantParagraphs(OpenXmlElement element)
    {
        var paragraphs = element.Descendants<DParagraph>()
            .Select(ExtractParagraphText)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();

        return string.Join("\n", paragraphs);
    }

    private sealed record PositionedBlock(string Text, long X, long Y, int OrderIndex);
}

