using PRN222.RagAssistant.Infrastructure.Billing;
using Xunit;

namespace PRN222.RagAssistant.Tests;

public sealed class VnPayHashHelperTests
{
    private const string TestSecret = "EUKFJAYATVRRJBDQHUWZTJRCTCSSOYVY";

    [Fact]
    public void CreateSecureHash_SortsParametersAlphabetically_AndComputesValidHmacSha512()
    {
        // Arrange
        var parameters = new Dictionary<string, string?>
        {
            ["vnp_Version"] = "2.1.0",
            ["vnp_Command"] = "pay",
            ["vnp_TmnCode"] = "8OTYXKKM",
            ["vnp_Amount"] = "10000000",
            ["vnp_CurrCode"] = "VND",
            ["vnp_TxnRef"] = "ORDER_001",
            ["vnp_OrderInfo"] = "Mua gói 50 câu hỏi"
        };

        // Act
        var hash1 = VnPayHashHelper.CreateSecureHash(parameters, TestSecret);

        // Parameters in reverse order should yield exact same hash because helper sorts them
        var reversed = new Dictionary<string, string?>
        {
            ["vnp_TxnRef"] = "ORDER_001",
            ["vnp_OrderInfo"] = "Mua gói 50 câu hỏi",
            ["vnp_CurrCode"] = "VND",
            ["vnp_Amount"] = "10000000",
            ["vnp_TmnCode"] = "8OTYXKKM",
            ["vnp_Command"] = "pay",
            ["vnp_Version"] = "2.1.0"
        };
        var hash2 = VnPayHashHelper.CreateSecureHash(reversed, TestSecret);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(hash1));
        Assert.Equal(128, hash1.Length); // 64 bytes in hex = 128 characters
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
            ["vnp_TmnCode"] = "8OTYXKKM",
            ["vnp_Amount"] = "5000000",
            ["vnp_CurrCode"] = "VND",
            ["vnp_TxnRef"] = "TXN_12345",
            ["vnp_OrderInfo"] = "Nap 50 cau hoi",
            ["vnp_ReturnUrl"] = "https://localhost:7001/Billing/Return"
        };

        var fullUrl = VnPayHashHelper.BuildFullUrl("https://sandbox.vnpayment.vn/paymentv2/vpcpay.html", parameters, TestSecret);

        Assert.StartsWith("https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?", fullUrl);
        Assert.Contains("vnp_TmnCode=8OTYXKKM", fullUrl);
        Assert.Contains("vnp_Amount=5000000", fullUrl);
        Assert.Contains("vnp_SecureHash=", fullUrl);
    }

    [Fact]
    public void VerifySecureHash_ValidSignature_ReturnsTrue()
    {
        var parameters = new Dictionary<string, string?>
        {
            ["vnp_Amount"] = "5000000",
            ["vnp_TmnCode"] = "8OTYXKKM",
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
            ["vnp_TmnCode"] = "8OTYXKKM",
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
            ["vnp_TmnCode"] = "8OTYXKKM",
            ["vnp_TxnRef"] = "TXN_777"
        };

        var hash = VnPayHashHelper.CreateSecureHash(parameters, TestSecret);
        parameters["vnp_SecureHash"] = hash;

        // Tamper with amount
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
            ["vnp_TmnCode"] = "8OTYXKKM"
        };

        var hash = VnPayHashHelper.CreateSecureHash(parameters, TestSecret);
        parameters["vnp_SecureHash"] = hash;

        var isValid = VnPayHashHelper.VerifySecureHash(parameters, "WRONG_SECRET_KEY");

        Assert.False(isValid);
    }
}
