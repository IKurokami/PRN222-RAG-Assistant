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
        var client = _httpClientFactory.CreateClient("Ollama");

        var request = new OllamaEmbedRequest(_model, text);

        var response = await client.PostAsJsonAsync("api/embed", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaEmbedResponse>(cancellationToken);

        if (result?.Embeddings is null || result.Embeddings.Count == 0)
        {
            throw new InvalidOperationException("Ollama returned no embeddings.");
        }

        _logger.LogDebug("Generated embedding with {Dimensions} dimensions for text of length {Length}",
            result.Embeddings[0].Length, text.Length);

        return result.Embeddings[0];
    }

    private sealed record OllamaEmbedRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input")] string Input);

    private sealed class OllamaEmbedResponse
    {
        [JsonPropertyName("embeddings")]
        public List<float[]>? Embeddings { get; set; }
    }
}
