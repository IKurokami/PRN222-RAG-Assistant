namespace PRN222.RagAssistant.Infrastructure.Rag;

/// <summary>
/// Provider capability used by the RAG orchestrator when the selected model supports
/// function/tool calling. Tool handlers are defined by the application so authorization
/// scope remains server-side and is never chosen by the model.
/// </summary>
public interface IAgenticChatCompletionService
{
    IAsyncEnumerable<string> StreamWithToolsAsync(
        string systemPrompt,
        string userPrompt,
        IReadOnlyList<AgentToolDefinition> tools,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Provider-neutral description of a server-side function that can be exposed to an AI model.
/// Microsoft.Extensions.AI is used by concrete providers to derive JSON schemas from the delegate.
/// </summary>
public sealed record AgentToolDefinition(
    string Name,
    string Description,
    Delegate Handler);
