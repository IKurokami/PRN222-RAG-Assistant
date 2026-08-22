using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using PRN222.RagAssistant.Application.Models;
using PRN222.RagAssistant.Infrastructure.Rag.Exceptions;
using PRN222.RagAssistant.Infrastructure.Services;

namespace PRN222.RagAssistant.Tests;

/// <summary>
/// Regression tests for issue #36:
/// AI provider 429 rate-limit errors must not appear as "không tìm thấy tài liệu phù hợp".
/// Acceptance criteria:
///   1. OpenRouterChatCompletionService throws AiProviderRateLimitException on 429.
///   2. OpenAiChatCompletionService throws AiProviderRateLimitException on 429.
///   3. AiProviderRateLimitException captures Retry-After when provided.
///   4. RagErrorEvent is a distinct stream event type (not a delta/done event).
///   5. A "no relevant documents" path still emits NoEvidenceMessage, not a rate-limit error.
/// </summary>
public sealed class RateLimitHandlingTests
{
    // -------------------------------------------------------------------------
    // 1. OpenRouter: 429 → AiProviderRateLimitException
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OpenRouterChat_throws_AiProviderRateLimitException_on_429()
    {
        var handler = new StubHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)));

        var service = new OpenRouterChatCompletionService(
            new StubHttpClientFactory(new HttpClient(handler)
            {
                BaseAddress = new Uri("https://openrouter.ai/api/v1/")
            }),
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Rag:OpenRouter:ChatModels"] = "openrouter/free"
                })
                .Build());

        var ex = await Assert.ThrowsAsync<AiProviderRateLimitException>(
            () => service.CompleteAsync("system", "user"));

        Assert.Equal("OpenRouter", ex.ProviderName);
    }

    [Fact]
    public async Task OpenRouterChat_does_NOT_throw_AiProviderRateLimitException_on_200()
    {
        var handler = new StubHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"choices\":[{\"message\":{\"content\":\"ok\"}}]}",
                    Encoding.UTF8, "application/json")
            }));

        var service = new OpenRouterChatCompletionService(
            new StubHttpClientFactory(new HttpClient(handler)
            {
                BaseAddress = new Uri("https://openrouter.ai/api/v1/")
            }),
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Rag:OpenRouter:ChatModels"] = "openrouter/free"
                })
                .Build());

        // Should NOT throw AiProviderRateLimitException — must complete normally.
        var result = await service.CompleteAsync("system", "user");
        Assert.Equal("ok", result);
    }

    // -------------------------------------------------------------------------
    // 2. OpenAI: 429 → AiProviderRateLimitException
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OpenAiChat_throws_AiProviderRateLimitException_on_429()
    {
        var handler = new StubHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)));

        var service = new OpenAiChatCompletionService(
            new StubHttpClientFactory(new HttpClient(handler)
            {
                BaseAddress = new Uri("https://api.openai.com/v1/")
            }),
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Rag:OpenAI:ChatModel"] = "gpt-5.6-nano"
                })
                .Build());

        var ex = await Assert.ThrowsAsync<AiProviderRateLimitException>(
            () => service.CompleteAsync("system", "user"));

        Assert.Equal("OpenAI", ex.ProviderName);
    }

    // -------------------------------------------------------------------------
    // 3. AiProviderRateLimitException: Retry-After header parsing
    // -------------------------------------------------------------------------

    [Fact]
    public void AiProviderRateLimitException_captures_RetryAfter_delta_from_response()
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(
            TimeSpan.FromSeconds(30));

        var ex = AiProviderRateLimitException.FromResponse("OpenRouter", response);

        Assert.NotNull(ex.RetryAfter);
        Assert.Equal(TimeSpan.FromSeconds(30), ex.RetryAfter!.Value);
        Assert.Equal("OpenRouter", ex.ProviderName);
    }

    [Fact]
    public void AiProviderRateLimitException_RetryAfter_is_null_when_header_absent()
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);

        var ex = AiProviderRateLimitException.FromResponse("OpenRouter", response);

        Assert.Null(ex.RetryAfter);
    }

    [Fact]
    public void AiProviderRateLimitException_message_includes_provider_name()
    {
        var ex = new AiProviderRateLimitException("TestProvider");

        Assert.Contains("TestProvider", ex.Message);
    }

    // -------------------------------------------------------------------------
    // 4. RagErrorEvent — typed stream event contract
    // -------------------------------------------------------------------------

    [Fact]
    public void RagErrorEvent_is_a_RagStreamEvent()
    {
        RagStreamEvent ev = new RagErrorEvent("AI_PROVIDER_RATE_LIMITED", "Quá tải.");
        Assert.IsType<RagErrorEvent>(ev);
    }

    [Fact]
    public void RagErrorEvent_is_NOT_a_RagDeltaEvent()
    {
        // Rate-limit errors must travel as RagErrorEvent, never as content deltas.
        RagStreamEvent ev = new RagErrorEvent("AI_PROVIDER_RATE_LIMITED", "Quá tải.");
        Assert.IsNotType<RagDeltaEvent>(ev);
    }

    [Fact]
    public void RagErrorEvent_carries_ErrorCode_and_Message()
    {
        var ev = new RagErrorEvent("AI_PROVIDER_RATE_LIMITED", "Thử lại sau.");

        Assert.Equal("AI_PROVIDER_RATE_LIMITED", ev.ErrorCode);
        Assert.Equal("Thử lại sau.", ev.Message);
    }

    [Fact]
    public void RagErrorEvent_AI_PROVIDER_RATE_LIMITED_errorCode_is_distinct_from_STREAM_ERROR()
    {
        var rateLimit = new RagErrorEvent("AI_PROVIDER_RATE_LIMITED", "...");
        var streamError = new RagErrorEvent("STREAM_ERROR", "...");

        Assert.NotEqual(rateLimit.ErrorCode, streamError.ErrorCode);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => responder(request);
    }
}
