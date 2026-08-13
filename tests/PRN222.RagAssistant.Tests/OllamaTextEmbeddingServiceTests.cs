using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PRN222.RagAssistant.Infrastructure.Services;

namespace PRN222.RagAssistant.Tests;

public sealed class OllamaTextEmbeddingServiceTests
{
    [Fact]
    public async Task EmbedBatchAsync_SendsOneArrayRequestAndPreservesResponseOrder()
    {
        string? requestJson = null;
        var handler = new StubHttpMessageHandler(async request =>
        {
            requestJson = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"embeddings\":[[1.0,2.0],[3.0,4.0]]}",
                    Encoding.UTF8,
                    "application/json")
            };
        });
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Rag:Ollama:EmbeddingModel"] = "test-embedding-model"
            })
            .Build();
        var service = new OllamaTextEmbeddingService(
            new StubHttpClientFactory(client),
            configuration,
            NullLogger<OllamaTextEmbeddingService>.Instance);

        var embeddings = await service.EmbedBatchAsync(["first", "second"]);

        Assert.Equal(2, embeddings.Count);
        Assert.Equal([1.0f, 2.0f], embeddings[0]);
        Assert.Equal([3.0f, 4.0f], embeddings[1]);
        Assert.Contains("\"input\":[\"first\",\"second\"]", requestJson);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task EmbedBatchAsync_RejectsMismatchedResponseCount()
    {
        var handler = new StubHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"embeddings\":[[1.0,2.0]]}",
                Encoding.UTF8,
                "application/json")
        }));
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Rag:Ollama:EmbeddingModel"] = "test-embedding-model"
            })
            .Build();
        var service = new OllamaTextEmbeddingService(
            new StubHttpClientFactory(client),
            configuration,
            NullLogger<OllamaTextEmbeddingService>.Instance);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.EmbedBatchAsync(["first", "second"]));

        Assert.Contains("1 embeddings for 2 inputs", exception.Message);
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return responder(request);
        }
    }
}
