using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using PRN222.RagAssistant.Application.Abstractions;

namespace PRN222.RagAssistant.Infrastructure.Services;

public sealed class OpenRouterTextEmbeddingService : ITextEmbeddingService
{
    private const string DefaultEmbeddingModel =
        "nvidia/llama-nemotron-embed-vl-1b-v2:free";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OpenRouterTextEmbeddingService> _logger;
    private readonly string _model;
    private readonly int _expectedDimensions;

    public OpenRouterTextEmbeddingService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<OpenRouterTextEmbeddingService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _model = configuration["Rag:OpenRouter:EmbeddingModel"]
            ?? configuration["OPENROUTER_EMBEDDING_MODEL"]
            ?? DefaultEmbeddingModel;
        _expectedDimensions = EmbeddingServiceGuard.GetExpectedDimensions(configuration);
    }

    public async Task<float[]> EmbedAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var result = await EmbedBatchAsync([text], cancellationToken);
        return result[0];
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        if (texts.Count == 0)
        {
            return [];
        }

        var client = _httpClientFactory.CreateClient("OpenRouter");
        var request = new
        {
            model = _model,
            input = texts,
            dimensions = _expectedDimensions,
            encoding_format = "float",
            provider = new
            {
                allow_fallbacks = true
            }
        };

        using var response = await client.PostAsJsonAsync(
            "embeddings",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<OpenRouterEmbeddingResponse>(
            cancellationToken: cancellationToken);
        var items = payload?.Data ?? [];
        var embeddings = items
            .OrderBy(item => item.Index)
            .Select(item => item.Embedding)
            .ToArray();

        _logger.LogDebug(
            "Generated {EmbeddingCount} embeddings with OpenRouter model {Model} at {Dimensions} dimensions.",
            embeddings.Length,
            _model,
            _expectedDimensions);

        return EmbeddingServiceGuard.ValidateResponse(
            "OpenRouter",
            embeddings,
            texts.Count,
            _expectedDimensions);
    }

    private sealed record OpenRouterEmbeddingResponse(
        [property: JsonPropertyName("data")] IReadOnlyList<OpenRouterEmbeddingItem> Data,
        [property: JsonPropertyName("model")] string? Model);

    private sealed record OpenRouterEmbeddingItem(
        [property: JsonPropertyName("index")] int Index,
        [property: JsonPropertyName("embedding")] float[] Embedding);
}
