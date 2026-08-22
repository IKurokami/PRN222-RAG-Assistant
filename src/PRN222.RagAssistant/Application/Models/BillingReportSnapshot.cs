namespace PRN222.RagAssistant.Application.Models;

public sealed class BillingReportSnapshot
{
    public int TotalOrders { get; init; }
    public int PaidOrders { get; init; }
    public int PendingOrders { get; init; }
    public int FailedOrders { get; init; }
    public int StalePendingOrders { get; init; }

    public long GrossPaidRevenueVnd { get; init; }
    public long PaidRevenueLast30DaysVnd { get; init; }
    public int PaidOrdersLast30Days { get; init; }
    public double AveragePaidOrderValueVnd { get; init; }
    public double SettledPaymentSuccessPercent { get; init; }
    public double CheckoutCompletionPercent { get; init; }

    public int UniquePayingUsers { get; init; }
    public int PurchasedQuotaUnits { get; init; }
    public double AverageQuotaUnitsPerPaidOrder { get; init; }
    public double EffectiveRevenuePerQuotaUnitVnd { get; init; }
    public int PaidOrdersMissingQuotaMetadata { get; init; }

    public int SubjectAttributedPaidOrders { get; init; }
    public int UnattributedPaidOrders { get; init; }

    public int RegisteredUsers { get; init; }
    public int UsersWithAvailableQuota { get; init; }
    public long TotalOutstandingQuota { get; init; }

    public IReadOnlyList<ReportDailyBillingActivity> DailyActivityLast7Days { get; init; } =
        Array.Empty<ReportDailyBillingActivity>();
    public IReadOnlyList<ReportQuotaPackageBreakdown> QuotaPackageMix { get; init; } =
        Array.Empty<ReportQuotaPackageBreakdown>();
    public IReadOnlyList<ReportPaymentChannelBreakdown> PaymentsByBank { get; init; } =
        Array.Empty<ReportPaymentChannelBreakdown>();
    public IReadOnlyList<ReportPaymentChannelBreakdown> PaymentsByCardType { get; init; } =
        Array.Empty<ReportPaymentChannelBreakdown>();
    public IReadOnlyList<ReportRecentPaymentOrder> RecentOrders { get; init; } =
        Array.Empty<ReportRecentPaymentOrder>();
}

public sealed class ReportDailyBillingActivity
{
    public DateTime DateUtc { get; init; }
    public int CreatedOrders { get; init; }
    public int PaidOrders { get; init; }
    public long PaidRevenueVnd { get; init; }
    public int PurchasedQuotaUnits { get; init; }
}

public sealed class ReportQuotaPackageBreakdown
{
    public int QuotaUnits { get; init; }
    public int PaidOrders { get; init; }
    public long PaidRevenueVnd { get; init; }
    public double PaidOrderSharePercent { get; init; }
}

public sealed class ReportPaymentChannelBreakdown
{
    public string Label { get; init; } = string.Empty;
    public int PaidOrders { get; init; }
    public long PaidRevenueVnd { get; init; }
    public double PaidOrderSharePercent { get; init; }
}

public sealed class ReportRecentPaymentOrder
{
    public string ExternalOrderId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public long AmountVnd { get; init; }
    public int? QuotaUnits { get; init; }
    public DateTime CreatedUtc { get; init; }
    public DateTime? PaidUtc { get; init; }
    public string? BankCode { get; init; }
    public string? CardType { get; init; }
    public bool IsSubjectAttributed { get; init; }
}
