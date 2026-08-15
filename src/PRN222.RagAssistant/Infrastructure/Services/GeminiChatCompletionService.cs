using System.Net.Http.Json;
using System.Text.Json.Serialization;
using PRN222.RagAssistant.Application.Abstractions;

namespace PRN222.RagAssistant.Infrastructure.Services;

public sealed class GeminiChatCompletionService : IChatCompletionService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _model;

    public GeminiChatCompletionService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _model = configuration["Rag:Gemini:ChatModel"]
            ?? throw new InvalidOperationException("Rag:Gemini:ChatModel must be configured.");
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

        var request = new
        {
            systemInstruction = new
            {
                parts = new[] { new { text = systemPrompt } }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = userPrompt } }
                }
            }
        };

        var client = _httpClientFactory.CreateClient("Gemini");
        using var response = await client.PostAsJsonAsync(
            $"v1beta/models/{Uri.EscapeDataString(_model)}:generateContent",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<GeminiChatResponse>(cancellationToken);
        var content = string.Join(
                string.Empty,
                result?.Candidates?.FirstOrDefault()?.Content?.Parts?
                    .Select(part => part.Text ?? string.Empty) ?? [])
            .Trim();

        return !string.IsNullOrWhiteSpace(content)
            ? content
            : throw new InvalidOperationException("Gemini returned an empty chat completion.");
    }

    private sealed class GeminiChatResponse
    {
        [JsonPropertyName("candidates")]
        public List<GeminiCandidate>? Candidates { get; set; }
    }

    private sealed class GeminiCandidate
    {
        [JsonPropertyName("content")]
        public GeminiContent? Content { get; set; }
    }

    private sealed class GeminiContent
    {
        [JsonPropertyName("parts")]
        public List<GeminiPart>? Parts { get; set; }
    }

    private sealed class GeminiPart
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }
}
