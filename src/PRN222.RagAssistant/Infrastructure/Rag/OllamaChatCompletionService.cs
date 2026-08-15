using System.Net.Http.Json;
using System.Text.Json.Serialization;
using PRN222.RagAssistant.Application.Abstractions;

namespace PRN222.RagAssistant.Infrastructure.Rag;

public sealed class OllamaChatCompletionService : IChatCompletionService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _model;
    private readonly ILogger<OllamaChatCompletionService> _logger;

    public OllamaChatCompletionService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<OllamaChatCompletionService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _model = configuration["Rag:Ollama:ChatModel"]
            ?? throw new InvalidOperationException("Rag:Ollama:ChatModel must be configured.");
        _logger = logger;
    }

    public async Task<string> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("Ollama");

        var request = new OllamaChatRequest
        {
            Model = _model,
            Messages = new[]
            {
                new OllamaMessage { Role = "system", Content = systemPrompt },
                new OllamaMessage { Role = "user", Content = userPrompt }
            },
            Stream = false
        };

        var response = await client.PostAsJsonAsync("api/chat", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(cancellationToken);

        if (result?.Message?.Content is null)
        {
            throw new InvalidOperationException("Ollama returned an empty response.");
        }

        _logger.LogDebug("Chat completion received {TokenCount} tokens", result.TotalDuration);

        return result.Message.Content;
    }

    private sealed class OllamaMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    private sealed class OllamaChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public OllamaMessage[] Messages { get; set; } = Array.Empty<OllamaMessage>();

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }
    }

    private sealed class OllamaChatResponse
    {
        [JsonPropertyName("message")]
        public OllamaMessage? Message { get; set; }

        [JsonPropertyName("total_duration")]
        public long TotalDuration { get; set; }
    }
}
