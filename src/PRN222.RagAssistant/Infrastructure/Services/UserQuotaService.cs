using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Infrastructure.Rag.Exceptions;

namespace PRN222.RagAssistant.Infrastructure.Services;

public sealed class UserQuotaService(ApplicationDbContext dbContext) : IUserQuotaService
{
    private static readonly AsyncLocal<ReservationState?> CurrentReservation = new();

    public async Task<int> GetRemainingQuotaAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        return user?.QuotaRemaining ?? 0;
    }

    public async Task<bool> HasQuotaAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (CurrentReservation.Value is { } reservation && reservation.UserId == userId)
        {
            return true;
        }

        return await GetRemainingQuotaAsync(userId, cancellationToken) > 0;
    }

    public async Task<IAsyncDisposable> ReserveQuotaAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (CurrentReservation.Value is not null)
        {
            throw new InvalidOperationException("Nested quota reservations are not supported.");
        }

        await DecrementQuotaAtomicallyAsync(userId, cancellationToken);

        var state = new ReservationState(userId);
        CurrentReservation.Value = state;
        return new ReservationLease(this, state);
    }

    public async Task ConsumeQuotaAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (CurrentReservation.Value is { } reservation && reservation.UserId == userId)
        {
            reservation.Committed = true;
            return;
        }

        await DecrementQuotaAtomicallyAsync(userId, cancellationToken);
    }

    public async Task GrantQuotaAsync(
        Guid userId,
        int quotaUnits,
        CancellationToken cancellationToken = default)
    {
        if (quotaUnits <= 0)
        {
            return;
        }

        if (dbContext.Database.IsRelational())
        {
            var affected = await dbContext.Users
                .Where(u => u.Id == userId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        u => u.QuotaRemaining,
                        u => u.QuotaRemaining + quotaUnits),
                    cancellationToken);

            if (affected != 1)
            {
                throw new InvalidOperationException($"Cannot grant quota because user '{userId}' does not exist.");
            }

            return;
        }

        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException($"Cannot grant quota because user '{userId}' does not exist.");
        user.QuotaRemaining += quotaUnits;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task DecrementQuotaAtomicallyAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (dbContext.Database.IsRelational())
        {
            var affected = await dbContext.Users
                .Where(u => u.Id == userId && u.QuotaRemaining > 0)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        u => u.QuotaRemaining,
                        u => u.QuotaRemaining - 1),
                    cancellationToken);

            if (affected != 1)
            {
                throw new InsufficientQuotaException(userId);
            }

            return;
        }

        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null || user.QuotaRemaining <= 0)
        {
            throw new InsufficientQuotaException(userId);
        }

        user.QuotaRemaining--;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async ValueTask ReleaseReservationAsync(ReservationState state)
    {
        try
        {
            if (!state.Committed)
            {
                await GrantQuotaAsync(state.UserId, 1, CancellationToken.None);
            }
        }
        finally
        {
            if (ReferenceEquals(CurrentReservation.Value, state))
            {
                CurrentReservation.Value = null;
            }
        }
    }

    private sealed class ReservationState(Guid userId)
    {
        public Guid UserId { get; } = userId;
        public bool Committed { get; set; }
    }

    private sealed class ReservationLease(UserQuotaService owner, ReservationState state) : IAsyncDisposable
    {
        private int _disposed;

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                await owner.ReleaseReservationAsync(state);
            }
        }
    }
}
