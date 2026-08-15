using System.Net.Http.Json;
using System.Text.Json.Serialization;
using PRN222.RagAssistant.Application.Abstractions;

namespace PRN222.RagAssistant.Infrastructure.Services;

public sealed class OpenAiTextEmbeddingService : ITextEmbeddingService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _model;
    private readonly int _expectedDimensions;
    private readonly ILogger<OpenAiTextEmbeddingService> _logger;

    public OpenAiTextEmbeddingService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<OpenAiTextEmbeddingService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _model = configuration["Rag:OpenAI:EmbeddingModel"]
            ?? throw new InvalidOperationException("Rag:OpenAI:EmbeddingModel must be configured.");
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

        var client = _httpClientFactory.CreateClient("OpenAI");
        var request = new
        {
            model = _model,
            input = texts,
            dimensions = _expectedDimensions,
            encoding_format = "float"
        };

        using var response = await client.PostAsJsonAsync("embeddings", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OpenAiEmbeddingResponse>(cancellationToken);
        var ordered = result?.Data?
            .OrderBy(item => item.Index)
            .Select(item => item.Embedding)
            .ToArray() ?? [];

        EmbeddingServiceGuard.ValidateResponse(
            "OpenAI",
            ordered,
            texts.Count,
            _expectedDimensions);

        _logger.LogDebug(
            "Generated {EmbeddingCount} OpenAI embeddings with {Dimensions} dimensions",
            ordered.Length,
            _expectedDimensions);

        return ordered;
    }

    private sealed class OpenAiEmbeddingResponse
    {
        [JsonPropertyName("data")]
        public List<OpenAiEmbeddingItem>? Data { get; set; }
    }

    private sealed class OpenAiEmbeddingItem
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("embedding")]
        public float[] Embedding { get; set; } = [];
    }
}
