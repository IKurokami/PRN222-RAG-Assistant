using System;
using System.IO;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Infrastructure.Parsing;
using Xunit;
using DParagraph = DocumentFormat.OpenXml.Drawing.Paragraph;
using DRun = DocumentFormat.OpenXml.Drawing.Run;
using DText = DocumentFormat.OpenXml.Drawing.Text;
using DTable = DocumentFormat.OpenXml.Drawing.Table;
using DTableRow = DocumentFormat.OpenXml.Drawing.TableRow;
using DTableCell = DocumentFormat.OpenXml.Drawing.TableCell;
using DTransform2D = DocumentFormat.OpenXml.Drawing.Transform2D;
using DOffset = DocumentFormat.OpenXml.Drawing.Offset;
using DExtents = DocumentFormat.OpenXml.Drawing.Extents;
using PShape = DocumentFormat.OpenXml.Presentation.Shape;
using PGraphicFrame = DocumentFormat.OpenXml.Presentation.GraphicFrame;
using WDocument = DocumentFormat.OpenXml.Wordprocessing.Document;
using WBody = DocumentFormat.OpenXml.Wordprocessing.Body;
using WParagraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using WRun = DocumentFormat.OpenXml.Wordprocessing.Run;
using WText = DocumentFormat.OpenXml.Wordprocessing.Text;
using WTable = DocumentFormat.OpenXml.Wordprocessing.Table;
using WTableRow = DocumentFormat.OpenXml.Wordprocessing.TableRow;
using WTableCell = DocumentFormat.OpenXml.Wordprocessing.TableCell;

namespace PRN222.RagAssistant.Tests;

public sealed class DocumentParserIntegrationTests
{
    [Fact]
    public void Docx_WithTableAndList_ParsesAndChunksIntoDocumentChunks()
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new WDocument(new WBody(
                new WParagraph(new WRun(new WText("Chapter 1: Overview of C#"))),
                new WParagraph(new WRun(new WText("C# is a modern, object-oriented language."))),
                new WTable(
                    new WTableRow(
                        new WTableCell(new WParagraph(new WRun(new WText("Header A")))),
                        new WTableCell(new WParagraph(new WRun(new WText("Header B"))))
                    ),
                    new WTableRow(
                        new WTableCell(new WParagraph(new WRun(new WText("Value 1")))),
                        new WTableCell(new WParagraph(new WRun(new WText("Value 2"))))
                    )
                ),
                new WParagraph(new WRun(new WText("Concluding remarks.")))
            ));
            mainPart.Document.Save();
        }

        stream.Position = 0;
        var parser = new DocxDocumentParser();
        var pages = parser.Parse(stream);

        Assert.Single(pages);
        var page = pages[0];
        Assert.Null(page.PageNumber);
        Assert.Contains("Chapter 1: Overview of C#", page.Text);
        Assert.Contains("| Header A | Header B |", page.Text);
        Assert.Contains("| Value 1 | Value 2 |", page.Text);

        var chunker = TextChunker.Create(maxChunkSize: 500, overlapSize: 100);
        var chunked = chunker.Chunk(pages);

        Assert.NotEmpty(chunked);
        var documentId = Guid.NewGuid();
        var documentChunks = chunked.Select(c => new DocumentChunk
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            ChunkIndex = c.ChunkIndex,
            Content = c.Content,
            PageNumber = c.PageNumber,
            SlideNumber = c.SlideNumber
        }).ToList();

        Assert.All(documentChunks, dc => Assert.Null(dc.PageNumber));
        Assert.Contains(documentChunks, dc => dc.Content.Contains("Header A"));
    }

    [Fact]
    public void Pptx_WithPositionedShapesAndTable_PreservesVisualOrderInChunks()
    {
        using var stream = new MemoryStream();
        using (var presentationDoc = PresentationDocument.Create(stream, PresentationDocumentType.Presentation, true))
        {
            var presPart = presentationDoc.AddPresentationPart();
            presPart.Presentation = new Presentation(new SlideIdList());

            var slidePart = presPart.AddNewPart<SlidePart>();
            var slideId = new SlideId { Id = 256, RelationshipId = presPart.GetIdOfPart(slidePart) };
            presPart.Presentation.SlideIdList!.Append(slideId);


            // Shape 2: Bottom shape (Y=2000000)
            var bottomShape = new PShape(
                new DocumentFormat.OpenXml.Presentation.ShapeProperties(
                    new DTransform2D(
                        new DOffset { X = 100000, Y = 2000000 },
                        new DExtents { Cx = 500000, Cy = 100000 }
                    )
                ),
                new DocumentFormat.OpenXml.Presentation.TextBody(
                    new DocumentFormat.OpenXml.Drawing.BodyProperties(),
                    new DParagraph(new DRun(new DText("Bottom Footnote Shape")))
                )
            );

            // Shape 1: Top Title shape (Y=100000)
            var topShape = new PShape(
                new DocumentFormat.OpenXml.Presentation.ShapeProperties(
                    new DTransform2D(
                        new DOffset { X = 100000, Y = 100000 },
                        new DExtents { Cx = 500000, Cy = 100000 }
                    )
                ),
                new DocumentFormat.OpenXml.Presentation.TextBody(
                    new DocumentFormat.OpenXml.Drawing.BodyProperties(),
                    new DParagraph(new DRun(new DText("Slide Title Topic")))
                )
            );

            slidePart.Slide = new Slide(
                new CommonSlideData(
                    new ShapeTree(
                        new DocumentFormat.OpenXml.Presentation.NonVisualGroupShapeProperties(
                            new DocumentFormat.OpenXml.Presentation.NonVisualDrawingProperties { Id = 1, Name = "" },
                            new DocumentFormat.OpenXml.Presentation.NonVisualGroupShapeDrawingProperties(),
                            new DocumentFormat.OpenXml.Presentation.ApplicationNonVisualDrawingProperties()),
                        new DocumentFormat.OpenXml.Presentation.GroupShapeProperties(),
                        bottomShape, // Added first in XML
                        topShape    // Added second in XML, but visually at TOP
                    )
                )
            );
            slidePart.Slide.Save();
            presPart.Presentation.Save();
        }

        stream.Position = 0;
        var parser = new PptxDocumentParser();
        var pages = parser.Parse(stream);

        var slide = Assert.Single(pages);
        Assert.Equal(1, slide.SlideNumber);
        Assert.Null(slide.PageNumber);

        // Title (top) must appear before Footnote (bottom) despite XML order
        var titleIdx = slide.Text.IndexOf("Slide Title Topic", StringComparison.Ordinal);
        var footnoteIdx = slide.Text.IndexOf("Bottom Footnote Shape", StringComparison.Ordinal);
        Assert.True(titleIdx < footnoteIdx, "Top shape must appear before bottom shape in visual reading order");

        var chunker = TextChunker.Create(maxChunkSize: 500, overlapSize: 100);
        var chunks = chunker.Chunk(pages);

        Assert.Single(chunks);
        Assert.Equal(1, chunks[0].SlideNumber);
        Assert.Null(chunks[0].PageNumber);
    }
}
