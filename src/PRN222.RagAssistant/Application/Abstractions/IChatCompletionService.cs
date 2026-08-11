namespace PRN222.RagAssistant.Application.Abstractions;

/// <summary>
/// Generates a chat completion without coupling the RAG workflow to a concrete model provider.
/// </summary>
public interface IChatCompletionService
{
    Task<string> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default);
}
