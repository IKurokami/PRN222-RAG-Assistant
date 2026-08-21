using System.Net;
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Infrastructure.Rag;

namespace PRN222.RagAssistant.Infrastructure.Services;

/// <summary>
/// Gemini chat provider backed by Google's official Google.GenAI SDK and the
/// Microsoft.Extensions.AI abstraction. The SDK owns HTTP/SSE parsing, while
/// Microsoft.Extensions.AI owns function schema generation and invocation loops.
/// Multiple configured Gemini models are tried in priority order when a model is
/// rate-limited, quota-exhausted, unavailable or otherwise fails transiently.
/// </summary>
public sealed class GeminiChatCompletionService :
    IChatCompletionService,
    IStreamingChatCompletionService,
    IAgenticChatCompletionService,
    IDisposable
{
    private readonly Client _genAiClient;
    private readonly IReadOnlyList<ModelClients> _modelClients;
    private readonly ILogger<GeminiChatCompletionService> _logger;

    public GeminiChatCompletionService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<GeminiChatCompletionService> logger)
        : this(configuration, () => httpClientFactory.CreateClient("Gemini"), logger)
    {
    }

    // Kept for lightweight direct construction in tests and utilities.
    public GeminiChatCompletionService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
        : this(
            configuration,
            () => httpClientFactory.CreateClient("Gemini"),
            NullLogger<GeminiChatCompletionService>.Instance)
    {
    }

    // Kept for lightweight direct construction outside DI. Production DI uses the
    // IHttpClientFactory overload above so SDK traffic participates in configured
    // handlers, timeouts and test doubles.
    public GeminiChatCompletionService(IConfiguration configuration)
        : this(
            configuration,
            httpClientFactory: null,
            NullLogger<GeminiChatCompletionService>.Instance)
    {
    }

    private GeminiChatCompletionService(
        IConfiguration configuration,
        Func<HttpClient>? httpClientFactory,
        ILogger<GeminiChatCompletionService> logger)
    {
        _logger = logger;
        var models = ResolveChatModels(configuration);
        if (models.Count == 0)
        {
            throw new InvalidOperationException(
                "Rag:Gemini:ChatModels or Rag:Gemini:ChatModel must be configured.");
        }

        // AddInfrastructure already validates the key for production. A placeholder is
        // allowed only when a custom HttpClient factory is supplied so isolated unit
        // tests can exercise SDK serialization/deserialization without a real credential.
        var apiKey = configuration["Rag:Gemini:ApiKey"]
            ?? configuration["GEMINI_API_KEY"]
            ?? (httpClientFactory is not null
                ? "test-sdk-key"
                : throw new InvalidOperationException(
                    "Rag:Gemini:ApiKey / GEMINI_API_KEY must be configured."));

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
            },
            clientOptions: httpClientFactory is null
                ? null
                : new ClientOptions
                {
                    HttpClientFactory = httpClientFactory
                });

        _modelClients = models
            .Select(model =>
            {
                var chatClient = _genAiClient.AsIChatClient(model);
                var agentClient = chatClient
                    .AsBuilder()
                    .UseFunctionInvocation(configure: invoker =>
                    {
                        // DbContext-backed RAG tools share the request scope, so keep tool
                        // execution sequential even if a model emits parallel tool calls.
                        invoker.AllowConcurrentInvocation = false;
                        invoker.MaximumIterationsPerRequest = 8;
                    })
                    .Build();

                return new ModelClients(model, chatClient, agentClient);
            })
            .ToList();
    }

    public async Task<string> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default)
    {
        ValidateUserPrompt(userPrompt);
        var messages = CreateMessages(systemPrompt, userPrompt);

        for (var index = 0; index < _modelClients.Count; index++)
        {
            var current = _modelClients[index];
            try
            {
                var response = await current.ChatClient.GetResponseAsync(
                    messages,
                    cancellationToken: cancellationToken);

                var content = response.Text.Trim();
                return !string.IsNullOrWhiteSpace(content)
                    ? content
                    : throw new InvalidOperationException(
                        $"Gemini model '{current.Model}' returned an empty chat completion.");
            }
            catch (Exception exception) when (
                index < _modelClients.Count - 1
                && ShouldFallback(exception, cancellationToken))
            {
                LogFallback(current.Model, _modelClients[index + 1].Model, exception);
            }
        }

        throw new InvalidOperationException("Gemini model fallback chain ended unexpectedly.");
    }

    public async IAsyncEnumerable<string> StreamAsync(
        string systemPrompt,
        string userPrompt,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        ValidateUserPrompt(userPrompt);
        var messages = CreateMessages(systemPrompt, userPrompt);

        await foreach (var delta in StreamWithFallbackAsync(
                           model => model.ChatClient.GetStreamingResponseAsync(
                               messages,
                               cancellationToken: cancellationToken),
                           cancellationToken))
        {
            yield return delta;
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
        var messages = CreateMessages(systemPrompt, userPrompt);

        await foreach (var delta in StreamWithFallbackAsync(
                           model => model.AgentClient.GetStreamingResponseAsync(
                               messages,
                               options,
                               cancellationToken),
                           cancellationToken))
        {
            yield return delta;
        }
    }

    private async IAsyncEnumerable<string> StreamWithFallbackAsync(
        Func<ModelClients, IAsyncEnumerable<ChatResponseUpdate>> streamFactory,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < _modelClients.Count; index++)
        {
            var current = _modelClients[index];
            Exception? fallbackException = null;
            var emittedText = false;

            await using var enumerator = streamFactory(current)
                .GetAsyncEnumerator(cancellationToken);

            while (true)
            {
                ChatResponseUpdate update;
                try
                {
                    if (!await enumerator.MoveNextAsync())
                    {
                        yield break;
                    }

                    update = enumerator.Current;
                }
                catch (Exception exception) when (
                    !emittedText
                    && index < _modelClients.Count - 1
                    && ShouldFallback(exception, cancellationToken))
                {
                    fallbackException = exception;
                    break;
                }

                if (!string.IsNullOrEmpty(update.Text))
                {
                    emittedText = true;
                    yield return update.Text;
                }
            }

            if (fallbackException is null)
            {
                yield break;
            }

            LogFallback(current.Model, _modelClients[index + 1].Model, fallbackException);
        }
    }

    private void LogFallback(string failedModel, string nextModel, Exception exception)
    {
        _logger.LogWarning(
            exception,
            "Gemini model {FailedModel} failed with a transient/quota error. Falling back to {NextModel}.",
            failedModel,
            nextModel);
    }

    private static bool ShouldFallback(
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        if (exception is HttpRequestException httpException)
        {
            return httpException.StatusCode is null
                or HttpStatusCode.NotFound
                or HttpStatusCode.RequestTimeout
                or HttpStatusCode.TooManyRequests
                or HttpStatusCode.InternalServerError
                or HttpStatusCode.BadGateway
                or HttpStatusCode.ServiceUnavailable
                or HttpStatusCode.GatewayTimeout;
        }

        if (exception is TimeoutException
            || exception is TaskCanceledException)
        {
            return true;
        }

        if (IsFallbackMessage(exception.Message))
        {
            return true;
        }

        return exception.InnerException is not null
            && ShouldFallback(exception.InnerException, cancellationToken);
    }

    private static bool IsFallbackMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        string[] markers =
        [
            "RESOURCE_EXHAUSTED",
            "quota exceeded",
            "rate limit",
            "too many requests",
            "429",
            "UNAVAILABLE",
            "temporarily unavailable",
            "deadline exceeded",
            "timed out",
            "timeout",
            "NOT_FOUND"
        ];

        return markers.Any(marker =>
            message.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> ResolveChatModels(IConfiguration configuration)
    {
        var models = new List<string>();
        var modelsSection = configuration.GetSection("Rag:Gemini:ChatModels");

        AddModels(models, modelsSection.Value);
        foreach (var child in modelsSection.GetChildren())
        {
            AddModels(models, child.Value);
        }

        if (models.Count == 0)
        {
            AddModels(models, configuration["Rag:Gemini:ChatModel"]);
        }

        return models
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddModels(ICollection<string> models, string? rawModels)
    {
        if (string.IsNullOrWhiteSpace(rawModels))
        {
            return;
        }

        foreach (var model in rawModels.Split(
                     [',', ';', '\n', '\r'],
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!string.IsNullOrWhiteSpace(model))
            {
                models.Add(model);
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
        foreach (var model in _modelClients)
        {
            model.AgentClient.Dispose();
        }

        _genAiClient.Dispose();
    }

    private sealed record ModelClients(
        string Model,
        IChatClient ChatClient,
        IChatClient AgentClient);
}
