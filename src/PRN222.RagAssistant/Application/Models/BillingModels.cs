namespace PRN222.RagAssistant.Application.Models;

public sealed record CreateBillingOrderRequest(
    Guid UserId,
    Guid? SubjectId,
    long Amount,
    string Currency,
    string Description,
    Uri ReturnUrl,
    string IpAddress);

public sealed record BillingOrderResult(
    Guid OrderId,
    string ExternalOrderId,
    Uri CheckoutUrl);

public sealed record BillingOrderStatus(
    Guid OrderId,
    Guid UserId,
    Guid? SubjectId,
    string Provider,
    string ExternalOrderId,
    string? ExternalTransactionNo,
    long Amount,
    string Currency,
    string Status,
    DateTime CreatedUtc,
    DateTime? PaidUtc,
    string? ExternalResponseCode,
    string? ErrorMessage,
    string? BankCode,
    string? CardType);

public sealed record ProcessReturnRequest(
    Guid OrderId,
    IReadOnlyDictionary<string, string?> CallbackParameters);

public sealed record ProcessWebhookRequest(
    string Provider,
    IReadOnlyDictionary<string, string?> CallbackParameters);

public sealed record BillingWebhookResult(
    bool Success,
    string Message);
