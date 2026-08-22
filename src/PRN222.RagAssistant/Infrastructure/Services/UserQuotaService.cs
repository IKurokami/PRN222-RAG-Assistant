using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Infrastructure.Rag.Exceptions;

namespace PRN222.RagAssistant.Infrastructure.Services;

public sealed class UserQuotaService(ApplicationDbContext dbContext) : IUserQuotaService
{
    private static readonly AsyncLocal<ReservationLease?> CurrentReservation = new();

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

    public async Task<IQuotaReservation> ReserveQuotaAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await DecrementQuotaAtomicallyAsync(userId, cancellationToken);
        return new ReservationLease(this, userId);
    }

    public async Task ConsumeQuotaAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (CurrentReservation.Value is { } reservation && reservation.UserId == userId)
        {
            reservation.Commit();
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

    private async ValueTask ReleaseReservationAsync(ReservationLease reservation)
    {
        try
        {
            if (!reservation.IsCommitted)
            {
                await GrantQuotaAsync(reservation.UserId, 1, CancellationToken.None);
            }
        }
        finally
        {
            if (ReferenceEquals(CurrentReservation.Value, reservation))
            {
                CurrentReservation.Value = null;
            }
        }
    }

    private sealed class ReservationLease(UserQuotaService owner, Guid userId) : IQuotaReservation
    {
        private int _activated;
        private int _committed;
        private int _disposed;

        public Guid UserId { get; } = userId;
        public bool IsCommitted => Volatile.Read(ref _committed) != 0;

        public void Activate()
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                throw new ObjectDisposedException(nameof(ReservationLease));
            }

            var current = CurrentReservation.Value;
            if (current is not null && !ReferenceEquals(current, this))
            {
                throw new InvalidOperationException("Nested quota reservations are not supported.");
            }

            CurrentReservation.Value = this;
            Volatile.Write(ref _activated, 1);
        }

        public void Commit()
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                throw new ObjectDisposedException(nameof(ReservationLease));
            }

            Interlocked.Exchange(ref _committed, 1);
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                await owner.ReleaseReservationAsync(this);
            }
        }
    }
}
