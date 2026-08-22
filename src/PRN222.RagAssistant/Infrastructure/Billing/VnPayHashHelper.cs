using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace PRN222.RagAssistant.Infrastructure.Billing;

public sealed class VnPayCompare : IComparer<string>
{
    private static readonly CompareInfo CompareInfo = CompareInfo.GetCompareInfo("en-US");

    public int Compare(string? x, string? y)
    {
        if (x == y) return 0;
        if (x is null) return -1;
        if (y is null) return 1;
        return CompareInfo.Compare(x, y, CompareOptions.Ordinal);
    }
}

public static class VnPayHashHelper
{
    private static readonly VnPayCompare Sorter = new();

    public static string CreateSecureHash(
        IReadOnlyDictionary<string, string?> parameters,
        string hashSecret) =>
        HmacSha512(hashSecret, BuildSignData(parameters));

    public static string BuildSignData(IReadOnlyDictionary<string, string?> parameters)
    {
        var sorted = parameters
            .Where(kv => kv.Key.StartsWith("vnp_", StringComparison.Ordinal)
                         && !string.IsNullOrEmpty(kv.Value))
            .OrderBy(kv => kv.Key, Sorter);

        var builder = new StringBuilder();
        foreach (var (key, value) in sorted)
        {
            if (IsHashField(key))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append('&');
            }

            builder.Append(WebUtility.UrlEncode(key));
            builder.Append('=');
            builder.Append(WebUtility.UrlEncode(value));
        }

        return builder.ToString();
    }

    public static string BuildFullUrl(
        string baseUrl,
        IReadOnlyDictionary<string, string?> parameters,
        string hashSecret)
    {
        var sorted = parameters
            .Where(kv => kv.Key.StartsWith("vnp_", StringComparison.Ordinal)
                         && !string.IsNullOrEmpty(kv.Value))
            .OrderBy(kv => kv.Key, Sorter);

        var query = new StringBuilder();
        var signData = new StringBuilder();

        foreach (var (key, value) in sorted)
        {
            if (IsHashField(key))
            {
                continue;
            }

            var encodedKey = WebUtility.UrlEncode(key);
            var encodedValue = WebUtility.UrlEncode(value);

            if (query.Length > 0)
            {
                query.Append('&');
                signData.Append('&');
            }

            query.Append(encodedKey).Append('=').Append(encodedValue);
            signData.Append(encodedKey).Append('=').Append(encodedValue);
        }

        var secureHash = HmacSha512(hashSecret, signData.ToString());
        var separator = baseUrl.Contains('?') ? "&" : "?";
        return $"{baseUrl}{separator}{query}&vnp_SecureHash={secureHash}";
    }

    public static bool VerifySecureHash(
        IReadOnlyDictionary<string, string?> parameters,
        string hashSecret)
    {
        if (!parameters.TryGetValue("vnp_SecureHash", out var providedHash)
            || string.IsNullOrWhiteSpace(providedHash))
        {
            return false;
        }

        return VerifySecureHash(parameters, hashSecret, providedHash);
    }

    public static bool VerifySecureHash(
        IReadOnlyDictionary<string, string?> parameters,
        string hashSecret,
        string providedHash)
    {
        if (string.IsNullOrWhiteSpace(providedHash))
        {
            return false;
        }

        var expectedHash = HmacSha512(hashSecret, BuildSignData(parameters));
        return FixedTimeHexEquals(expectedHash, providedHash.Trim());
    }

    public static string HmacSha512(string key, string inputData)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key));
        var hashValue = hmac.ComputeHash(Encoding.UTF8.GetBytes(inputData));
        return Convert.ToHexString(hashValue).ToLowerInvariant();
    }

    private static bool FixedTimeHexEquals(string expected, string provided)
    {
        if (expected.Length != provided.Length)
        {
            return false;
        }

        try
        {
            var expectedBytes = Convert.FromHexString(expected);
            var providedBytes = Convert.FromHexString(provided);
            return CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsHashField(string key) =>
        string.Equals(key, "vnp_SecureHash", StringComparison.OrdinalIgnoreCase)
        || string.Equals(key, "vnp_SecureHashType", StringComparison.OrdinalIgnoreCase);
}
