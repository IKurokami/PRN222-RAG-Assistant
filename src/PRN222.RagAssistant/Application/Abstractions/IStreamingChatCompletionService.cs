namespace PRN222.RagAssistant.Application.Abstractions;

/// <summary>
/// Optional capability for chat providers that can expose model output incrementally.
/// Providers that do not implement this interface continue to work through
/// <see cref="IChatCompletionService"/>.
/// </summary>
public interface IStreamingChatCompletionService
{
    IAsyncEnumerable<string> StreamAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default);
}
