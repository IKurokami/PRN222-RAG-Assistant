using PRN222.RagAssistant.Infrastructure.Billing;
using Xunit;

namespace PRN222.RagAssistant.Tests;

public sealed class VnPayHashHelperTests
{
    private const string TestSecret = "test-vnpay-hash-secret-for-unit-tests-only";
    private const string TestTmnCode = "TESTTMN001";

    [Fact]
    public void CreateSecureHash_SortsParametersAlphabetically_AndComputesValidHmacSha512()
    {
        var parameters = new Dictionary<string, string?>
        {
            ["vnp_Version"] = "2.1.0",
            ["vnp_Command"] = "pay",
            ["vnp_TmnCode"] = TestTmnCode,
            ["vnp_Amount"] = "10000000",
            ["vnp_CurrCode"] = "VND",
            ["vnp_TxnRef"] = "ORDER_001",
            ["vnp_OrderInfo"] = "Mua gói 50 câu hỏi"
        };

        var hash1 = VnPayHashHelper.CreateSecureHash(parameters, TestSecret);

        var reversed = new Dictionary<string, string?>
        {
            ["vnp_TxnRef"] = "ORDER_001",
            ["vnp_OrderInfo"] = "Mua gói 50 câu hỏi",
            ["vnp_CurrCode"] = "VND",
            ["vnp_Amount"] = "10000000",
            ["vnp_TmnCode"] = TestTmnCode,
            ["vnp_Command"] = "pay",
            ["vnp_Version"] = "2.1.0"
        };
        var hash2 = VnPayHashHelper.CreateSecureHash(reversed, TestSecret);

        Assert.False(string.IsNullOrWhiteSpace(hash1));
        Assert.Equal(128, hash1.Length);
        Assert.Equal(hash1, hash2);
        Assert.Equal(hash1, hash1.ToLowerInvariant());
    }

    [Fact]
    public void BuildFullUrl_IncludesAllParametersAndSecureHash()
    {
        var parameters = new Dictionary<string, string?>
        {
            ["vnp_Version"] = "2.1.0",
            ["vnp_Command"] = "pay",
            ["vnp_TmnCode"] = TestTmnCode,
            ["vnp_Amount"] = "5000000",
            ["vnp_CurrCode"] = "VND",
            ["vnp_TxnRef"] = "TXN_12345",
            ["vnp_OrderInfo"] = "Nap 50 cau hoi",
            ["vnp_ReturnUrl"] = "https://localhost:7001/Billing/Return"
        };

        var fullUrl = VnPayHashHelper.BuildFullUrl(
            "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
            parameters,
            TestSecret);

        Assert.StartsWith("https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?", fullUrl);
        Assert.Contains($"vnp_TmnCode={TestTmnCode}", fullUrl);
        Assert.Contains("vnp_Amount=5000000", fullUrl);
        Assert.Contains("vnp_SecureHash=", fullUrl);
    }

    [Fact]
    public void VerifySecureHash_ValidSignature_ReturnsTrue()
    {
        var parameters = new Dictionary<string, string?>
        {
            ["vnp_Amount"] = "5000000",
            ["vnp_TmnCode"] = TestTmnCode,
            ["vnp_TxnRef"] = "TXN_777",
            ["vnp_OrderInfo"] = "Gói ôn thi PRN222"
        };

        var hash = VnPayHashHelper.CreateSecureHash(parameters, TestSecret);
        parameters["vnp_SecureHash"] = hash;

        var isValid = VnPayHashHelper.VerifySecureHash(parameters, TestSecret);

        Assert.True(isValid);
    }

    [Fact]
    public void VerifySecureHash_CaseInsensitiveHash_ReturnsTrue()
    {
        var parameters = new Dictionary<string, string?>
        {
            ["vnp_Amount"] = "5000000",
            ["vnp_TmnCode"] = TestTmnCode,
            ["vnp_TxnRef"] = "TXN_777"
        };

        var hash = VnPayHashHelper.CreateSecureHash(parameters, TestSecret);
        parameters["vnp_SecureHash"] = hash.ToUpperInvariant();

        var isValid = VnPayHashHelper.VerifySecureHash(parameters, TestSecret);

        Assert.True(isValid);
    }

    [Fact]
    public void VerifySecureHash_TamperedParameter_ReturnsFalse()
    {
        var parameters = new Dictionary<string, string?>
        {
            ["vnp_Amount"] = "5000000",
            ["vnp_TmnCode"] = TestTmnCode,
            ["vnp_TxnRef"] = "TXN_777"
        };

        var hash = VnPayHashHelper.CreateSecureHash(parameters, TestSecret);
        parameters["vnp_SecureHash"] = hash;
        parameters["vnp_Amount"] = "9999999";

        var isValid = VnPayHashHelper.VerifySecureHash(parameters, TestSecret);

        Assert.False(isValid);
    }

    [Fact]
    public void VerifySecureHash_WrongSecret_ReturnsFalse()
    {
        var parameters = new Dictionary<string, string?>
        {
            ["vnp_Amount"] = "5000000",
            ["vnp_TmnCode"] = TestTmnCode
        };

        var hash = VnPayHashHelper.CreateSecureHash(parameters, TestSecret);
        parameters["vnp_SecureHash"] = hash;

        var isValid = VnPayHashHelper.VerifySecureHash(parameters, "different-test-secret");

        Assert.False(isValid);
    }
}
