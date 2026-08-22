using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Infrastructure.Rag.Exceptions;

namespace PRN222.RagAssistant.Infrastructure.Services;

public sealed class OpenRouterChatCompletionService : IChatCompletionService
{
    private const string DefaultChatModels =
        "google/gemma-4-26b-a4b-it:free,nvidia/nemotron-3-ultra-550b-a55b:free,openrouter/free";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IReadOnlyList<string> _models;

    public OpenRouterChatCompletionService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;

        var configuredModels = configuration["Rag:OpenRouter:ChatModels"]
            ?? configuration["OPENROUTER_CHAT_MODELS"]
            ?? DefaultChatModels;

        _models = configuredModels
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (_models.Count == 0)
        {
            throw new InvalidOperationException(
                "Rag:OpenRouter:ChatModels / OPENROUTER_CHAT_MODELS must contain at least one model ID.");
        }
    }

    public async Task<string> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("OpenRouter");
        var request = new
        {
            models = _models,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            provider = new
            {
                allow_fallbacks = true
            }
        };

        using var response = await client.PostAsJsonAsync(
            "chat/completions",
            request,
            cancellationToken);

        // Issue #36: detect rate-limit explicitly so callers can surface a
        // user-friendly message instead of the generic "no documents" fallback.
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw AiProviderRateLimitException.FromResponse("OpenRouter", response);
        }

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<OpenRouterChatResponse>(
            cancellationToken: cancellationToken);
        var content = payload?.Choices.FirstOrDefault()?.Message.Content;

        return !string.IsNullOrWhiteSpace(content)
            ? content
            : throw new InvalidOperationException(
                $"OpenRouter returned no chat content after trying the configured model chain: {string.Join(" -> ", _models)}.");
    }

    private sealed record OpenRouterChatResponse(
        [property: JsonPropertyName("choices")] IReadOnlyList<OpenRouterChoice> Choices,
        [property: JsonPropertyName("model")] string? Model);

    private sealed record OpenRouterChoice(
        [property: JsonPropertyName("message")] OpenRouterMessage Message);

    private sealed record OpenRouterMessage(
        [property: JsonPropertyName("content")] string? Content);
}
