namespace PRN222.RagAssistant.Application.Abstractions;

public interface IUserQuotaService
{
    Task<int> GetRemainingQuotaAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> HasQuotaAsync(Guid userId, CancellationToken cancellationToken = default);
    Task ConsumeQuotaAsync(Guid userId, CancellationToken cancellationToken = default);
    Task GrantQuotaAsync(Guid userId, int quotaUnits, CancellationToken cancellationToken = default);
}
