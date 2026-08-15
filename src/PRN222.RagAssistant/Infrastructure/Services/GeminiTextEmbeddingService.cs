using System.Net.Http.Json;
using System.Text.Json.Serialization;
using PRN222.RagAssistant.Application.Abstractions;

namespace PRN222.RagAssistant.Infrastructure.Services;

public sealed class GeminiTextEmbeddingService : ITextEmbeddingService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _model;
    private readonly int _expectedDimensions;
    private readonly ILogger<GeminiTextEmbeddingService> _logger;

    public GeminiTextEmbeddingService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<GeminiTextEmbeddingService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _model = configuration["Rag:Gemini:EmbeddingModel"]
            ?? throw new InvalidOperationException("Rag:Gemini:EmbeddingModel must be configured.");
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

        var modelResource = $"models/{_model}";
        var requests = texts.Select(text => new
        {
            model = modelResource,
            content = new
            {
                parts = new[] { new { text } }
            },
            embedContentConfig = new
            {
                outputDimensionality = _expectedDimensions
            }
        });

        var client = _httpClientFactory.CreateClient("Gemini");
        using var response = await client.PostAsJsonAsync(
            $"v1beta/models/{Uri.EscapeDataString(_model)}:batchEmbedContents",
            new { requests },
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<GeminiEmbeddingResponse>(cancellationToken);
        var embeddings = result?.Embeddings?
            .Select(item => item.Values)
            .ToArray() ?? [];

        EmbeddingServiceGuard.ValidateResponse(
            "Gemini",
            embeddings,
            texts.Count,
            _expectedDimensions);

        _logger.LogDebug(
            "Generated {EmbeddingCount} Gemini embeddings with {Dimensions} dimensions",
            embeddings.Length,
            _expectedDimensions);

        return embeddings;
    }

    private sealed class GeminiEmbeddingResponse
    {
        [JsonPropertyName("embeddings")]
        public List<GeminiEmbedding>? Embeddings { get; set; }
    }

    private sealed class GeminiEmbedding
    {
        [JsonPropertyName("values")]
        public float[] Values { get; set; } = [];
    }
}
