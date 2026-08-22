namespace PRN222.RagAssistant.Domain.Entities;

public sealed class PaymentOrder
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? SubjectId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ExternalOrderId { get; set; } = string.Empty;
    public string? ExternalTransactionNo { get; set; }
    public string? BankCode { get; set; }
    public string? CardType { get; set; }
    public long Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ExternalResponseCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? PaidUtc { get; set; }
    public string MetadataJson { get; set; } = string.Empty;
}
