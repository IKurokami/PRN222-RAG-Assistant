using System.Net.Http.Json;
using System.Text.Json.Serialization;
using PRN222.RagAssistant.Application.Abstractions;

namespace PRN222.RagAssistant.Infrastructure.Services;

public sealed class OpenAiChatCompletionService : IChatCompletionService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _model;

    public OpenAiChatCompletionService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _model = configuration["Rag:OpenAI:ChatModel"]
            ?? throw new InvalidOperationException("Rag:OpenAI:ChatModel must be configured.");
    }

    public async Task<string> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            throw new ArgumentException("User prompt must not be empty.", nameof(userPrompt));
        }

        var client = _httpClientFactory.CreateClient("OpenAI");
        var request = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        };

        using var response = await client.PostAsJsonAsync("chat/completions", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OpenAiChatResponse>(cancellationToken);
        var content = result?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();

        return !string.IsNullOrWhiteSpace(content)
            ? content
            : throw new InvalidOperationException("OpenAI returned an empty chat completion.");
    }

    private sealed class OpenAiChatResponse
    {
        [JsonPropertyName("choices")]
        public List<OpenAiChoice>? Choices { get; set; }
    }

    private sealed class OpenAiChoice
    {
        [JsonPropertyName("message")]
        public OpenAiMessage? Message { get; set; }
    }

    private sealed class OpenAiMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}
