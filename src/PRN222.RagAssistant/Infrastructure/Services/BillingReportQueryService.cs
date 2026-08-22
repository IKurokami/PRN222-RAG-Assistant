using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Application.Models;
using PRN222.RagAssistant.Data;

namespace PRN222.RagAssistant.Infrastructure.Services;

public sealed class BillingReportQueryService : IBillingReportQueryService
{
    private static readonly TimeSpan StalePendingAge = TimeSpan.FromMinutes(30);
    private readonly ApplicationDbContext _dbContext;

    public BillingReportQueryService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<BillingReportSnapshot> GetBillingReportAsync(
        CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var stalePendingCutoffUtc = nowUtc.Subtract(StalePendingAge);
        var thirtyDayWindowStartUtc = nowUtc.AddDays(-30);
        var activityWindowStartUtc = nowUtc.Date.AddDays(-6);

        var orders = await _dbContext.PaymentOrders
            .AsNoTracking()
            .Select(order => new PaymentReportRow
            {
                Id = order.Id,
                UserId = order.UserId,
                SubjectId = order.SubjectId,
                ExternalOrderId = order.ExternalOrderId,
                Amount = order.Amount,
                Status = order.Status,
                CreatedUtc = order.CreatedUtc,
                PaidUtc = order.PaidUtc,
                MetadataJson = order.MetadataJson,
                BankCode = order.BankCode,
                CardType = order.CardType
            })
            .ToListAsync(cancellationToken);

        var quotaBalances = await _dbContext.Users
            .AsNoTracking()
            .Select(user => user.QuotaRemaining)
            .ToListAsync(cancellationToken);

        var paidOrders = orders
            .Where(order => string.Equals(order.Status, "Paid", StringComparison.Ordinal))
            .ToList();
        var failedOrders = orders
            .Where(order => string.Equals(order.Status, "Failed", StringComparison.Ordinal))
            .ToList();
        var pendingOrders = orders
            .Where(order => string.Equals(order.Status, "Pending", StringComparison.Ordinal))
            .ToList();
        var stalePendingOrders = pendingOrders
            .Where(order => order.CreatedUtc <= stalePendingCutoffUtc)
            .ToList();

        var paidQuotaRows = paidOrders
            .Select(order => new PaidQuotaRow(order, TryGetQuotaUnits(order.MetadataJson)))
            .ToList();
        var validPaidQuotaRows = paidQuotaRows
            .Where(row => row.QuotaUnits.HasValue)
            .ToList();

        var grossPaidRevenueVnd = paidOrders.Sum(order => order.Amount);
        var validQuotaRevenueVnd = validPaidQuotaRows.Sum(row => row.Order.Amount);
        var purchasedQuotaUnits = validPaidQuotaRows.Sum(row => row.QuotaUnits!.Value);
        var paidOrdersLast30Days = paidOrders
            .Count(order => order.PaidUtc >= thirtyDayWindowStartUtc);
        var paidRevenueLast30DaysVnd = paidOrders
            .Where(order => order.PaidUtc >= thirtyDayWindowStartUtc)
            .Sum(order => order.Amount);

        var dailyActivityLast7Days = Enumerable.Range(0, 7)
            .Select(dayOffset =>
            {
                var dayStartUtc = activityWindowStartUtc.AddDays(dayOffset);
                var nextDayUtc = dayStartUtc.AddDays(1);
                var paidOnDay = paidOrders
                    .Where(order => order.PaidUtc >= dayStartUtc && order.PaidUtc < nextDayUtc)
                    .ToList();

                return new ReportDailyBillingActivity
                {
                    DateUtc = dayStartUtc,
                    CreatedOrders = orders.Count(order =>
                        order.CreatedUtc >= dayStartUtc && order.CreatedUtc < nextDayUtc),
                    PaidOrders = paidOnDay.Count,
                    PaidRevenueVnd = paidOnDay.Sum(order => order.Amount),
                    PurchasedQuotaUnits = validPaidQuotaRows
                        .Where(row => row.Order.PaidUtc >= dayStartUtc && row.Order.PaidUtc < nextDayUtc)
                        .Sum(row => row.QuotaUnits!.Value)
                };
            })
            .ToList();

        var quotaPackageMix = validPaidQuotaRows
            .GroupBy(row => row.QuotaUnits!.Value)
            .Select(group => new ReportQuotaPackageBreakdown
            {
                QuotaUnits = group.Key,
                PaidOrders = group.Count(),
                PaidRevenueVnd = group.Sum(row => row.Order.Amount),
                PaidOrderSharePercent = Ratio(group.Count(), validPaidQuotaRows.Count)
            })
            .OrderByDescending(item => item.PaidOrders)
            .ThenBy(item => item.QuotaUnits)
            .ToList();

        var recentOrders = orders
            .OrderByDescending(order => order.CreatedUtc)
            .Take(10)
            .Select(order => new ReportRecentPaymentOrder
            {
                ExternalOrderId = order.ExternalOrderId,
                Status = order.Status,
                AmountVnd = order.Amount,
                QuotaUnits = TryGetQuotaUnits(order.MetadataJson),
                CreatedUtc = order.CreatedUtc,
                PaidUtc = order.PaidUtc,
                BankCode = order.BankCode,
                CardType = order.CardType,
                IsSubjectAttributed = order.SubjectId.HasValue
            })
            .ToList();

        return new BillingReportSnapshot
        {
            TotalOrders = orders.Count,
            PaidOrders = paidOrders.Count,
            PendingOrders = pendingOrders.Count,
            FailedOrders = failedOrders.Count,
            StalePendingOrders = stalePendingOrders.Count,
            GrossPaidRevenueVnd = grossPaidRevenueVnd,
            PaidRevenueLast30DaysVnd = paidRevenueLast30DaysVnd,
            PaidOrdersLast30Days = paidOrdersLast30Days,
            AveragePaidOrderValueVnd = Average(grossPaidRevenueVnd, paidOrders.Count),
            SettledPaymentSuccessPercent = Ratio(paidOrders.Count, paidOrders.Count + failedOrders.Count),
            CheckoutCompletionPercent = Ratio(
                paidOrders.Count,
                paidOrders.Count + failedOrders.Count + stalePendingOrders.Count),
            UniquePayingUsers = paidOrders.Select(order => order.UserId).Distinct().Count(),
            PurchasedQuotaUnits = purchasedQuotaUnits,
            AverageQuotaUnitsPerPaidOrder = Average(purchasedQuotaUnits, validPaidQuotaRows.Count),
            EffectiveRevenuePerQuotaUnitVnd = Average(validQuotaRevenueVnd, purchasedQuotaUnits),
            PaidOrdersMissingQuotaMetadata = paidQuotaRows.Count(row => !row.QuotaUnits.HasValue),
            SubjectAttributedPaidOrders = paidOrders.Count(order => order.SubjectId.HasValue),
            UnattributedPaidOrders = paidOrders.Count(order => !order.SubjectId.HasValue),
            RegisteredUsers = quotaBalances.Count,
            UsersWithAvailableQuota = quotaBalances.Count(quota => quota > 0),
            TotalOutstandingQuota = quotaBalances.Sum(quota => (long)Math.Max(0, quota)),
            DailyActivityLast7Days = dailyActivityLast7Days,
            QuotaPackageMix = quotaPackageMix,
            PaymentsByBank = BuildChannelBreakdown(paidOrders, order => order.BankCode),
            PaymentsByCardType = BuildChannelBreakdown(paidOrders, order => order.CardType),
            RecentOrders = recentOrders
        };
    }

    private static IReadOnlyList<ReportPaymentChannelBreakdown> BuildChannelBreakdown(
        IReadOnlyCollection<PaymentReportRow> paidOrders,
        Func<PaymentReportRow, string?> labelSelector)
    {
        return paidOrders
            .Select(order => new
            {
                Order = order,
                Label = labelSelector(order)?.Trim()
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Label))
            .GroupBy(item => item.Label!, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ReportPaymentChannelBreakdown
            {
                Label = group.Key,
                PaidOrders = group.Count(),
                PaidRevenueVnd = group.Sum(item => item.Order.Amount),
                PaidOrderSharePercent = Ratio(group.Count(), paidOrders.Count)
            })
            .OrderByDescending(item => item.PaidOrders)
            .ThenByDescending(item => item.PaidRevenueVnd)
            .ThenBy(item => item.Label)
            .Take(8)
            .ToList();
    }

    private static int? TryGetQuotaUnits(string metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            if (document.RootElement.TryGetProperty("quotaUnits", out var quotaElement)
                && quotaElement.TryGetInt32(out var quotaUnits)
                && quotaUnits > 0)
            {
                return quotaUnits;
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private static double Ratio(long numerator, long denominator)
    {
        return denominator == 0
            ? 0
            : Math.Round(numerator * 100.0 / denominator, 1, MidpointRounding.AwayFromZero);
    }

    private static double Average(long numerator, long denominator)
    {
        return denominator == 0
            ? 0
            : Math.Round(numerator * 1.0 / denominator, 2, MidpointRounding.AwayFromZero);
    }

    private sealed class PaymentReportRow
    {
        public Guid Id { get; init; }
        public Guid UserId { get; init; }
        public Guid? SubjectId { get; init; }
        public string ExternalOrderId { get; init; } = string.Empty;
        public long Amount { get; init; }
        public string Status { get; init; } = string.Empty;
        public DateTime CreatedUtc { get; init; }
        public DateTime? PaidUtc { get; init; }
        public string MetadataJson { get; init; } = string.Empty;
        public string? BankCode { get; init; }
        public string? CardType { get; init; }
    }

    private sealed record PaidQuotaRow(PaymentReportRow Order, int? QuotaUnits);
}
