using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PRN222.RagAssistant.Application.Abstractions;

namespace PRN222.RagAssistant.Infrastructure.Services;

public sealed class OllamaTextEmbeddingService : ITextEmbeddingService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _model;
    private readonly ILogger<OllamaTextEmbeddingService> _logger;

    public OllamaTextEmbeddingService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<OllamaTextEmbeddingService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _model = configuration["Rag:Ollama:EmbeddingModel"]
            ?? throw new InvalidOperationException("Rag:Ollama:EmbeddingModel must be configured.");
        _logger = logger;
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        var embeddings = await EmbedBatchAsync([text], cancellationToken);
        return embeddings[0];
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        if (texts.Count == 0)
        {
            return [];
        }

        if (texts.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Embedding inputs must not contain empty text.", nameof(texts));
        }

        var client = _httpClientFactory.CreateClient("Ollama");
        var request = new OllamaEmbedRequest(_model, texts);

        using var response = await client.PostAsJsonAsync("api/embed", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaEmbedResponse>(cancellationToken);

        if (result?.Embeddings is null || result.Embeddings.Count != texts.Count)
        {
            throw new InvalidOperationException(
                $"Ollama returned {result?.Embeddings?.Count ?? 0} embeddings for {texts.Count} inputs.");
        }

        var dimensions = result.Embeddings[0].Length;
        if (dimensions == 0 || result.Embeddings.Any(embedding => embedding.Length != dimensions))
        {
            throw new InvalidOperationException("Ollama returned empty or inconsistent embedding dimensions.");
        }

        _logger.LogDebug(
            "Generated {EmbeddingCount} embeddings with {Dimensions} dimensions",
            result.Embeddings.Count,
            dimensions);

        return result.Embeddings;
    }

    private sealed record OllamaEmbedRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input")] IReadOnlyList<string> Input);

    private sealed class OllamaEmbedResponse
    {
        [JsonPropertyName("embeddings")]
        public List<float[]>? Embeddings { get; set; }
    }
}
