using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PRN222.RagAssistant.Data.Configurations;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Infrastructure.Services;

namespace PRN222.RagAssistant.Tests;

public sealed class GeminiEmbeddingCompatibilityTests
{
    [Fact]
    public async Task BatchEmbedding_PutsConfiguredDimensionsOnEachGeminiRequest()
    {
        string? requestJson = null;
        var handler = new StubHttpMessageHandler(async request =>
        {
            requestJson = await request.Content!.ReadAsStringAsync();
            return JsonResponse(
                "{\"embeddings\":[{\"values\":[1.0,2.0]},{\"values\":[3.0,4.0]}]}");
        });
        var service = new GeminiTextEmbeddingService(
            new StubHttpClientFactory(new HttpClient(handler)
            {
                BaseAddress = new Uri("https://generativelanguage.googleapis.com/")
            }),
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Rag:EmbeddingDimensions"] = "2",
                    ["Rag:Gemini:EmbeddingModel"] = "gemini-embedding-001"
                })
                .Build(),
            NullLogger<GeminiTextEmbeddingService>.Instance);

        var embeddings = await service.EmbedBatchAsync(["first", "second"]);

        Assert.Equal(2, embeddings.Count);
        Assert.NotNull(requestJson);

        using var payload = JsonDocument.Parse(requestJson!);
        var requests = payload.RootElement.GetProperty("requests");
        Assert.Equal(2, requests.GetArrayLength());

        foreach (var request in requests.EnumerateArray())
        {
            Assert.Equal("models/gemini-embedding-001", request.GetProperty("model").GetString());
            Assert.Equal(2, request.GetProperty("outputDimensionality").GetInt32());
            Assert.False(request.TryGetProperty("embedContentConfig", out _));
        }

        Assert.Equal(
            "/v1beta/models/gemini-embedding-001:batchEmbedContents",
            handler.LastRequestUri?.AbsolutePath);
    }

    [Fact]
    public void DocumentChunkEmbeddingColumn_DoesNotHardCodeVectorDimensions()
    {
        var modelBuilder = new ModelBuilder(new ConventionSet());
        new DocumentChunkConfiguration().Configure(modelBuilder.Entity<DocumentChunk>());

        var entityType = modelBuilder.Model.FindEntityType(typeof(DocumentChunk));
        var embeddingProperty = entityType?.FindProperty(nameof(DocumentChunk.Embedding));

        Assert.NotNull(embeddingProperty);
        Assert.Equal("vector", embeddingProperty!.GetColumnType());
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            return responder(request);
        }
    }
}
