using System.Globalization;
using System.Text;
using PRN222.RagAssistant.Infrastructure.Parsing;

namespace PRN222.RagAssistant.Tests;

public sealed class PdfDocumentParserTests
{
    [Fact]
    public void Parse_MinimalTextPdf_ReturnsPageContent()
    {
        using var stream = CreateMinimalPdf("Hello PRN222 PDF");

        var pages = new PdfDocumentParser().Parse(stream);

        var page = Assert.Single(pages);
        Assert.Equal(1, page.PageNumber);
        Assert.Contains("Hello PRN222 PDF", page.Text);
    }

    [Fact]
    public void Parse_PositionedText_ReconstructsReadingOrderAndWordSpacing()
    {
        const string contentStream = """
            BT
            /F1 12 Tf
            1 0 0 1 72 700 Tm
            (Second line remains readable.) Tj
            1 0 0 1 72 720 Tm
            (Chapter 11.) Tj
            1 0 0 1 145 720 Tm
            (The presence of XML.) Tj
            ET
            """;
        using var stream = CreateMinimalPdfFromContentStream(contentStream);

        var pages = new PdfDocumentParser().Parse(stream);

        var page = Assert.Single(pages);
        Assert.Equal(
            "Chapter 11. The presence of XML.\nSecond line remains readable.",
            page.Text);
    }

    private static MemoryStream CreateMinimalPdf(string text) =>
        CreateMinimalPdfFromContentStream($"BT /F1 12 Tf 72 720 Td ({text}) Tj ET");

    private static MemoryStream CreateMinimalPdfFromContentStream(string contentStream)
    {
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(contentStream)} >>\nstream\n{contentStream}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
        };
        var builder = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };

        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString()));
            builder.Append(CultureInfo.InvariantCulture, $"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(builder.ToString());
        builder.Append(CultureInfo.InvariantCulture, $"xref\n0 {objects.Length + 1}\n");
        builder.Append("0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
        {
            builder.Append(CultureInfo.InvariantCulture, $"{offset:0000000000} 00000 n \n");
        }

        builder.Append(CultureInfo.InvariantCulture,
            $"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");

        return new MemoryStream(Encoding.ASCII.GetBytes(builder.ToString()));
    }
}
