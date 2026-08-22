namespace PRN222.RagAssistant.Infrastructure.Billing;

public sealed class VnPayBillingOptions
{
    public const string SectionName = "Billing:VnPay";

    public string TmnCode { get; set; } = string.Empty;
    public string HashSecret { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
    public string Version { get; set; } = "2.1.0";
    public string Command { get; set; } = "pay";
    public string Locale { get; set; } = "vn";
    public string OrderType { get; set; } = "other";
}
