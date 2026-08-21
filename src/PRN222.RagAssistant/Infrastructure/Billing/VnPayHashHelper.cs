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
        string hashSecret)
    {
        var signData = BuildSignData(parameters);
        return HmacSha512(hashSecret, signData);
    }

    public static string BuildSignData(IReadOnlyDictionary<string, string?> parameters)
    {
        var sorted = parameters
            .Where(kv => !string.IsNullOrEmpty(kv.Value))
            .OrderBy(kv => kv.Key, Sorter);

        var sb = new StringBuilder();
        foreach (var (key, value) in sorted)
        {
            if (string.Equals(key, "vnp_SecureHash", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "vnp_SecureHashType", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (sb.Length > 0)
            {
                sb.Append('&');
            }

            sb.Append(WebUtility.UrlEncode(key));
            sb.Append('=');
            sb.Append(WebUtility.UrlEncode(value));
        }

        return sb.ToString();
    }

    public static string BuildFullUrl(
        string baseUrl,
        IReadOnlyDictionary<string, string?> parameters,
        string hashSecret)
    {
        var sorted = parameters
            .Where(kv => !string.IsNullOrEmpty(kv.Value))
            .OrderBy(kv => kv.Key, Sorter);

        var query = new StringBuilder();
        var signData = new StringBuilder();

        foreach (var (key, value) in sorted)
        {
            if (string.Equals(key, "vnp_SecureHash", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "vnp_SecureHashType", StringComparison.OrdinalIgnoreCase))
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

            query.Append(encodedKey);
            query.Append('=');
            query.Append(encodedValue);

            signData.Append(encodedKey);
            signData.Append('=');
            signData.Append(encodedValue);
        }

        var secureHash = HmacSha512(hashSecret, signData.ToString());
        var separator = baseUrl.Contains('?') ? "&" : "?";
        return $"{baseUrl}{separator}{query}&vnp_SecureHash={secureHash}";
    }

    public static bool VerifySecureHash(
        IReadOnlyDictionary<string, string?> parameters,
        string hashSecret)
    {
        if (!parameters.TryGetValue("vnp_SecureHash", out var providedHash) || string.IsNullOrWhiteSpace(providedHash))
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

        var signData = BuildSignData(parameters);
        var expectedHash = HmacSha512(hashSecret, signData);
        return string.Equals(expectedHash, providedHash.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static string HmacSha512(string key, string inputData)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key));
        var hashValue = hmac.ComputeHash(Encoding.UTF8.GetBytes(inputData));
        return Convert.ToHexString(hashValue).ToLowerInvariant();
    }
}
