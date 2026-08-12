using PRN222.RagAssistant.Infrastructure.Parsing;
using Xunit;

namespace PRN222.RagAssistant.Tests;

public sealed class DocumentParserFactoryTests
{
    [Theory]
    [InlineData(".pdf", typeof(PdfDocumentParser))]
    [InlineData(".docx", typeof(DocxDocumentParser))]
    [InlineData(".pptx", typeof(PptxDocumentParser))]
    [InlineData(".PDF", typeof(PdfDocumentParser))]
    public void GetParser_SupportedExtensions_ReturnsCorrectParser(string extension, Type expectedType)
    {
        var factory = new DocumentParserFactory();

        var parser = factory.GetParser(extension);

        Assert.IsType(expectedType, parser);
    }

    [Fact]
    public void GetParser_UnsupportedExtension_ThrowsNotSupportedException()
    {
        var factory = new DocumentParserFactory();

        Assert.Throws<NotSupportedException>(() => factory.GetParser(".txt"));
    }
}
