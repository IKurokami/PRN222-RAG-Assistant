namespace PRN222.RagAssistant.Infrastructure.Billing;

public sealed class BillingOptions
{
    public const string SectionName = "Billing";

    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
}
