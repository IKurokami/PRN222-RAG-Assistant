using System.Net;

namespace PRN222.RagAssistant.Infrastructure.Rag.Exceptions;

/// <summary>
/// Thrown when an AI provider returns HTTP 429 Too Many Requests.
/// Callers should surface a user-friendly rate-limit message rather than
/// a generic "no documents found" fallback.
/// </summary>
public sealed class AiProviderRateLimitException : RagException
{
    /// <summary>The provider that returned the rate-limit response (e.g. "OpenRouter", "Gemini").</summary>
    public string ProviderName { get; }

    /// <summary>
    /// Optional retry delay parsed from the <c>Retry-After</c> response header.
    /// <c>null</c> when the provider did not include the header.
    /// </summary>
    public TimeSpan? RetryAfter { get; }

    public AiProviderRateLimitException(string providerName, TimeSpan? retryAfter = null)
        : base(BuildMessage(providerName, retryAfter))
    {
        ProviderName = providerName;
        RetryAfter = retryAfter;
    }

    public AiProviderRateLimitException(string providerName, TimeSpan? retryAfter, Exception innerException)
        : base(BuildMessage(providerName, retryAfter), innerException)
    {
        ProviderName = providerName;
        RetryAfter = retryAfter;
    }

    private static string BuildMessage(string providerName, TimeSpan? retryAfter)
    {
        var baseMsg = $"AI provider '{providerName}' returned HTTP 429 Too Many Requests.";
        return retryAfter.HasValue
            ? $"{baseMsg} Retry after {retryAfter.Value.TotalSeconds:F0} seconds."
            : baseMsg;
    }

    /// <summary>
    /// Parses a <see cref="HttpResponseMessage"/> with status 429 and builds an
    /// <see cref="AiProviderRateLimitException"/>, extracting <c>Retry-After</c> when present.
    /// </summary>
    public static AiProviderRateLimitException FromResponse(string providerName, HttpResponseMessage response)
    {
        TimeSpan? retryAfter = null;

        if (response.Headers.RetryAfter is { } retryAfterHeader)
        {
            if (retryAfterHeader.Delta.HasValue)
            {
                retryAfter = retryAfterHeader.Delta.Value;
            }
            else if (retryAfterHeader.Date.HasValue)
            {
                var delay = retryAfterHeader.Date.Value - DateTimeOffset.UtcNow;
                if (delay > TimeSpan.Zero)
                {
                    retryAfter = delay;
                }
            }
        }

        return new AiProviderRateLimitException(providerName, retryAfter);
    }
}
