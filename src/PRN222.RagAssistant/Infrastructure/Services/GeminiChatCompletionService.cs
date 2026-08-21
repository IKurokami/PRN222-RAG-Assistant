using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.AI;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Infrastructure.Rag;

namespace PRN222.RagAssistant.Infrastructure.Services;

/// <summary>
/// Gemini chat provider backed by Google's official Google.GenAI SDK and the
/// Microsoft.Extensions.AI abstraction. The SDK owns HTTP/SSE parsing, while
/// Microsoft.Extensions.AI owns function schema generation and invocation loops.
/// </summary>
public sealed class GeminiChatCompletionService :
    IChatCompletionService,
    IStreamingChatCompletionService,
    IAgenticChatCompletionService,
    IDisposable
{
    private readonly Client _genAiClient;
    private readonly IChatClient _chatClient;
    private readonly IChatClient _agentClient;
    private readonly string _model;

    public GeminiChatCompletionService(IConfiguration configuration)
    {
        _model = configuration["Rag:Gemini:ChatModel"]
            ?? throw new InvalidOperationException("Rag:Gemini:ChatModel must be configured.");

        var apiKey = configuration["Rag:Gemini:ApiKey"]
            ?? configuration["GEMINI_API_KEY"]
            ?? throw new InvalidOperationException(
                "Rag:Gemini:ApiKey / GEMINI_API_KEY must be configured.");

        var baseUrl = configuration["Rag:Gemini:BaseUrl"]
            ?? configuration["GEMINI_BASE_URL"]
            ?? "https://generativelanguage.googleapis.com/";

        _genAiClient = new Client(
            apiKey: apiKey,
            httpOptions: new HttpOptions
            {
                BaseUrl = baseUrl.TrimEnd('/'),
                ApiVersion = "v1beta",
                Timeout = 120_000
            });

        _chatClient = _genAiClient.AsIChatClient(_model);
        _agentClient = _chatClient
            .AsBuilder()
            .UseFunctionInvocation(configure: invoker =>
            {
                // DbContext-backed RAG tools share the request scope, so keep tool
                // execution sequential even if a model emits parallel tool calls.
                invoker.AllowConcurrentInvocation = false;
                invoker.MaximumIterationsPerRequest = 8;
            })
            .Build();
    }

    public async Task<string> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default)
    {
        ValidateUserPrompt(userPrompt);

        var response = await _chatClient.GetResponseAsync(
            CreateMessages(systemPrompt, userPrompt),
            cancellationToken: cancellationToken);

        var content = response.Text.Trim();
        return !string.IsNullOrWhiteSpace(content)
            ? content
            : throw new InvalidOperationException("Gemini returned an empty chat completion.");
    }

    public async IAsyncEnumerable<string> StreamAsync(
        string systemPrompt,
        string userPrompt,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        ValidateUserPrompt(userPrompt);

        await foreach (var update in _chatClient.GetStreamingResponseAsync(
                           CreateMessages(systemPrompt, userPrompt),
                           cancellationToken: cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                yield return update.Text;
            }
        }
    }

    public async IAsyncEnumerable<string> StreamWithToolsAsync(
        string systemPrompt,
        string userPrompt,
        IReadOnlyList<AgentToolDefinition> tools,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        ValidateUserPrompt(userPrompt);

        var options = new ChatOptions
        {
            Tools = tools
                .Select(tool => (AITool)AIFunctionFactory.Create(
                    tool.Handler,
                    tool.Name,
                    tool.Description))
                .ToList()
        };

        await foreach (var update in _agentClient.GetStreamingResponseAsync(
                           CreateMessages(systemPrompt, userPrompt),
                           options,
                           cancellationToken))
        {
            // FunctionInvokingChatClient resolves intermediate function calls and
            // yields the model's final textual response incrementally.
            if (!string.IsNullOrEmpty(update.Text))
            {
                yield return update.Text;
            }
        }
    }

    private static List<ChatMessage> CreateMessages(string systemPrompt, string userPrompt) =>
    [
        new(ChatRole.System, systemPrompt),
        new(ChatRole.User, userPrompt)
    ];

    private static void ValidateUserPrompt(string userPrompt)
    {
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            throw new ArgumentException("User prompt must not be empty.", nameof(userPrompt));
        }
    }

    public void Dispose()
    {
        _agentClient.Dispose();
        _genAiClient.Dispose();
    }
}
