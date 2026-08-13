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

    private static MemoryStream CreateMinimalPdf(string text)
    {
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
            $"<< /Length {Encoding.ASCII.GetByteCount($"BT /F1 12 Tf 72 720 Td ({text}) Tj ET")} >>\nstream\nBT /F1 12 Tf 72 720 Td ({text}) Tj ET\nendstream",
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
