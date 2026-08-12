namespace PRN222.RagAssistant.Infrastructure.Parsing;

public interface IDocumentParser
{
    IReadOnlyList<ParsedPage> Parse(Stream fileStream);
}
