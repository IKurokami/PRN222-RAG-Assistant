using PRN222.RagAssistant.Application.Models;

namespace PRN222.RagAssistant.Application.Abstractions;

public interface IBillingReportQueryService
{
    Task<BillingReportSnapshot> GetBillingReportAsync(
        CancellationToken cancellationToken = default);
}
