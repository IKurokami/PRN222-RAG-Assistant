namespace PRN222.RagAssistant.Infrastructure.Parsing;

public sealed class DocumentParserFactory
{
    private static readonly PdfDocumentParser PdfParser = new();
    private static readonly DocxDocumentParser DocxParser = new();
    private static readonly PptxDocumentParser PptxParser = new();

    public IDocumentParser GetParser(string fileExtension)
    {
        return fileExtension.ToLowerInvariant() switch
        {
            ".pdf" => PdfParser,
            ".docx" => DocxParser,
            ".pptx" => PptxParser,
            _ => throw new NotSupportedException($"File extension '{fileExtension}' is not supported for indexing.")
        };
    }
}
