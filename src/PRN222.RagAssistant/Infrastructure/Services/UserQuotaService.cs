using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Infrastructure.Rag.Exceptions;

namespace PRN222.RagAssistant.Infrastructure.Services;

public sealed class UserQuotaService(ApplicationDbContext dbContext) : IUserQuotaService
{
    public async Task<int> GetRemainingQuotaAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        return user?.QuotaRemaining ?? 0;
    }

    public async Task<bool> HasQuotaAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var remaining = await GetRemainingQuotaAsync(userId, cancellationToken);
        return remaining > 0;
    }

    public async Task ConsumeQuotaAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null || user.QuotaRemaining <= 0)
        {
            throw new InsufficientQuotaException(userId);
        }

        user.QuotaRemaining--;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task GrantQuotaAsync(Guid userId, int quotaUnits, CancellationToken cancellationToken = default)
    {
        if (quotaUnits <= 0)
        {
            return;
        }

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
        {
            return;
        }

        user.QuotaRemaining += quotaUnits;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
