using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Infrastructure;
using PRN222.RagAssistant.Infrastructure.Services;

namespace PRN222.RagAssistant.Tests;

public sealed class AiProviderServicesTests
{
    [Theory]
    [InlineData("Ollama", typeof(OllamaTextEmbeddingService), typeof(OllamaChatCompletionService))]
    [InlineData("OpenAI", typeof(OpenAiTextEmbeddingService), typeof(OpenAiChatCompletionService))]
    [InlineData("Gemini", typeof(GeminiTextEmbeddingService), typeof(GeminiChatCompletionService))]
    public void AddInfrastructure_SelectsConfiguredAiProvider(
        string providerName,
        Type expectedEmbeddingType,
        Type expectedChatType)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(CreateProviderConfiguration(providerName));

        var embeddingDescriptor = Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(ITextEmbeddingService));
        var chatDescriptor = Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(IChatCompletionService));

        Assert.Equal(expectedEmbeddingType, embeddingDescriptor.ImplementationType);
        Assert.Equal(expectedChatType, chatDescriptor.ImplementationType);
    }

    [Fact]
    public void AddInfrastructure_RejectsOnlineProviderWithoutApiKey()
    {
        var configuration = CreateProviderConfiguration("OpenAI", includeApiKey: false);
        var services = new ServiceCollection();
        services.AddLogging();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddInfrastructure(configuration));

        Assert.Contains("Rag:OpenAI:ApiKey", exception.Message);
    }

    [Fact]
    public void AddInfrastructure_RejectsUnsupportedAiProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddInfrastructure(CreateProviderConfiguration("Unknown")));

        Assert.Contains("Supported values: Ollama, OpenAI, Gemini", exception.Message);
    }

    [Theory]
    [InlineData("OpenAI", "OpenAI", "Authorization", "Bearer test-openai-key")]
    [InlineData("Gemini", "Gemini", "x-goog-api-key", "test-gemini-key")]
    public void OnlineProvider_HttpClientCarriesServerSideApiKey(
        string providerName,
        string clientName,
        string headerName,
        string expectedValue)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(CreateProviderConfiguration(providerName));

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(clientName);

        var values = client.DefaultRequestHeaders.TryGetValues(headerName, out var headerValues)
            ? headerValues
            : [];

        Assert.Contains(expectedValue, values);
    }

    [Fact]
    public async Task OpenAiEmbedding_RequestsConfiguredDimensionsAndPreservesIndexOrder()
    {
        string? requestJson = null;
        var handler = new StubHttpMessageHandler(async request =>
        {
            requestJson = await request.Content!.ReadAsStringAsync();
            return JsonResponse(
                "{\"data\":[{\"index\":1,\"embedding\":[3.0,4.0]},{\"index\":0,\"embedding\":[1.0,2.0]}]}");
        });
        var service = new OpenAiTextEmbeddingService(
            new StubHttpClientFactory(new HttpClient(handler)
            {
                BaseAddress = new Uri("https://api.openai.com/v1/")
            }),
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Rag:EmbeddingDimensions"] = "2",
                    ["Rag:OpenAI:EmbeddingModel"] = "text-embedding-3-small"
                })
                .Build(),
            NullLogger<OpenAiTextEmbeddingService>.Instance);

        var embeddings = await service.EmbedBatchAsync(["first", "second"]);

        Assert.Equal([1.0f, 2.0f], embeddings[0]);
        Assert.Equal([3.0f, 4.0f], embeddings[1]);
        Assert.Contains("\"dimensions\":2", requestJson);
        Assert.Contains("\"model\":\"text-embedding-3-small\"", requestJson);
        Assert.Equal("/v1/embeddings", handler.LastRequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task GeminiEmbedding_UsesBatchEndpointAndConfiguredDimensions()
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
                    ["Rag:Gemini:EmbeddingModel"] = "gemini-embedding-2"
                })
                .Build(),
            NullLogger<GeminiTextEmbeddingService>.Instance);

        var embeddings = await service.EmbedBatchAsync(["first", "second"]);

        Assert.Equal(2, embeddings.Count);
        Assert.Contains("\"outputDimensionality\":2", requestJson);
        Assert.Contains("\"model\":\"models/gemini-embedding-2\"", requestJson);
        Assert.Equal(
            "/v1beta/models/gemini-embedding-2:batchEmbedContents",
            handler.LastRequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task OnlineChatServices_ParseProviderResponses()
    {
        var openAiHandler = new StubHttpMessageHandler(_ => Task.FromResult(
            JsonResponse("{\"choices\":[{\"message\":{\"content\":\"OpenAI answer\"}}]}")));
        var openAi = new OpenAiChatCompletionService(
            new StubHttpClientFactory(new HttpClient(openAiHandler)
            {
                BaseAddress = new Uri("https://api.openai.com/v1/")
            }),
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Rag:OpenAI:ChatModel"] = "gpt-5.6-luna"
                })
                .Build());

        var geminiHandler = new StubHttpMessageHandler(_ => Task.FromResult(
            JsonResponse("{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"Gemini answer\"}]}}]}")));
        var gemini = new GeminiChatCompletionService(
            new StubHttpClientFactory(new HttpClient(geminiHandler)
            {
                BaseAddress = new Uri("https://generativelanguage.googleapis.com/")
            }),
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Rag:Gemini:ChatModel"] = "gemini-3.6-flash"
                })
                .Build());

        Assert.Equal("OpenAI answer", await openAi.CompleteAsync("system", "question"));
        Assert.Equal("Gemini answer", await gemini.CompleteAsync("system", "question"));
    }

    private static IConfiguration CreateProviderConfiguration(
        string providerName,
        bool includeApiKey = true)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] =
                "Host=localhost;Port=5432;Database=prn222_test;Username=postgres;Password=postgres",
            ["Rag:Provider"] = providerName,
            ["Rag:EmbeddingDimensions"] = "1024",
            ["Rag:Ollama:BaseUrl"] = "http://localhost:11434",
            ["Rag:OpenAI:BaseUrl"] = "https://api.openai.com/v1/",
            ["Rag:Gemini:BaseUrl"] = "https://generativelanguage.googleapis.com/"
        };

        if (includeApiKey)
        {
            values["Rag:OpenAI:ApiKey"] = "test-openai-key";
            values["Rag:Gemini:ApiKey"] = "test-gemini-key";
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
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
