using PRN222.RagAssistant.Application.Models;

namespace PRN222.RagAssistant.Application.Abstractions;

public interface IBillingService
{
    Task<BillingOrderResult> CreateOrderAsync(CreateBillingOrderRequest request, CancellationToken cancellationToken);
    Task<BillingOrderStatus?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken);
    Task<BillingOrderStatus> ProcessReturnAsync(ProcessReturnRequest request, CancellationToken cancellationToken);
    Task<BillingWebhookResult> ProcessWebhookAsync(ProcessWebhookRequest request, CancellationToken cancellationToken);
}
