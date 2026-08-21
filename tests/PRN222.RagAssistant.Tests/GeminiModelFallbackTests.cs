using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using PRN222.RagAssistant.Infrastructure.Services;

namespace PRN222.RagAssistant.Tests;

public sealed class GeminiModelFallbackTests
{
    [Fact]
    public async Task CompleteAsync_FallsBackToNextModel_OnQuotaError()
    {
        var requestPaths = new List<string>();
        var handler = new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            requestPaths.Add(path);

            if (path.Contains("gemini-primary", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                {
                    Content = new StringContent(
                        "{\"error\":{\"code\":429,\"status\":\"RESOURCE_EXHAUSTED\",\"message\":\"Quota exceeded\"}}",
                        Encoding.UTF8,
                        "application/json")
                });
            }

            return Task.FromResult(JsonResponse(
                "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"fallback answer\"}]}}]}"));
        });

        using var service = CreateService(
            handler,
            "gemini-primary,gemini-secondary");

        var answer = await service.CompleteAsync("system", "question");

        Assert.Equal("fallback answer", answer);
        Assert.Contains(requestPaths, path => path.Contains("gemini-primary", StringComparison.Ordinal));
        Assert.Contains(requestPaths, path => path.Contains("gemini-secondary", StringComparison.Ordinal));
        Assert.True(
            requestPaths.FindIndex(path => path.Contains("gemini-primary", StringComparison.Ordinal))
            < requestPaths.FindIndex(path => path.Contains("gemini-secondary", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CompleteAsync_DoesNotFallback_OnBadRequest()
    {
        var requestPaths = new List<string>();
        var handler = new StubHttpMessageHandler(request =>
        {
            requestPaths.Add(request.RequestUri?.AbsolutePath ?? string.Empty);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(
                    "{\"error\":{\"code\":400,\"status\":\"INVALID_ARGUMENT\",\"message\":\"Bad request\"}}",
                    Encoding.UTF8,
                    "application/json")
            });
        });

        using var service = CreateService(
            handler,
            "gemini-primary,gemini-secondary");

        await Assert.ThrowsAnyAsync<Exception>(() =>
            service.CompleteAsync("system", "question"));

        Assert.NotEmpty(requestPaths);
        Assert.DoesNotContain(
            requestPaths,
            path => path.Contains("gemini-secondary", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CompleteAsync_StillSupportsLegacySingleChatModelSetting()
    {
        var handler = new StubHttpMessageHandler(_ => Task.FromResult(JsonResponse(
            "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"legacy answer\"}]}}]}")));
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com/")
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Rag:Gemini:ChatModel"] = "gemini-legacy"
            })
            .Build();

        using var service = new GeminiChatCompletionService(
            new StubHttpClientFactory(client),
            configuration);

        var answer = await service.CompleteAsync("system", "question");

        Assert.Equal("legacy answer", answer);
        Assert.Contains("gemini-legacy", handler.LastRequestUri?.AbsolutePath);
    }

    private static GeminiChatCompletionService CreateService(
        HttpMessageHandler handler,
        string models)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com/")
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Rag:Gemini:ChatModels"] = models
            })
            .Build();

        return new GeminiChatCompletionService(
            new StubHttpClientFactory(client),
            configuration);
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
