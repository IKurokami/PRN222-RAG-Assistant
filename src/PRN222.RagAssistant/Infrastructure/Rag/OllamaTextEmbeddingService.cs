using System.Net.Http.Json;
using System.Text.Json.Serialization;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Infrastructure.Services;

namespace PRN222.RagAssistant.Infrastructure.Rag;

public sealed class OllamaTextEmbeddingService : ITextEmbeddingService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _model;
    private readonly int _expectedDimensions;
    private readonly ILogger<OllamaTextEmbeddingService> _logger;

    public OllamaTextEmbeddingService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<OllamaTextEmbeddingService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _model = configuration["Rag:Ollama:EmbeddingModel"]
            ?? throw new InvalidOperationException("Rag:Ollama:EmbeddingModel must be configured.");
        _expectedDimensions = EmbeddingServiceGuard.GetExpectedDimensions(configuration);
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
        var embeddings = result?.Embeddings ?? [];

        EmbeddingServiceGuard.ValidateResponse(
            "Ollama",
            embeddings,
            texts.Count,
            _expectedDimensions);

        _logger.LogDebug(
            "Generated {EmbeddingCount} Ollama embeddings with {Dimensions} dimensions",
            embeddings.Count,
            _expectedDimensions);

        return embeddings;
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
