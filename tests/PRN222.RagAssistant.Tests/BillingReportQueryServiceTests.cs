using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Infrastructure.Services;
using PRN222.RagAssistant.Pages.Reports;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Tests;

public sealed class BillingReportQueryServiceTests
{
    [Fact]
    public void Billing_report_page_is_admin_only()
    {
        var authorizeAttributes = typeof(BillingModel)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .ToList();

        Assert.Contains(authorizeAttributes, attribute =>
            string.Equals(attribute.Roles, AppRoles.Admin, StringComparison.Ordinal));
    }

    [Fact]
    public void Billing_report_page_depends_on_query_service_instead_of_db_context()
    {
        var constructor = Assert.Single(typeof(BillingModel).GetConstructors());
        var parameterTypes = constructor.GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToList();

        Assert.Contains(typeof(IBillingReportQueryService), parameterTypes);
        Assert.DoesNotContain(typeof(ApplicationDbContext), parameterTypes);
    }

    [Fact]
    public async Task GetBillingReportAsync_calculates_paid_revenue_quota_conversion_and_channels()
    {
        await using var dbContext = CreateContext();
        var now = DateTime.UtcNow;
        var userAId = Guid.NewGuid();
        var userBId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        dbContext.Users.AddRange(
            CreateUser(userAId, "buyer-a", quotaRemaining: 7),
            CreateUser(userBId, "buyer-b", quotaRemaining: 0));
        dbContext.Subjects.Add(new Subject
        {
            Id = subjectId,
            Code = "PAY",
            Name = "Payment attribution test",
            IsActive = true
        });

        dbContext.PaymentOrders.AddRange(
            CreateOrder(userAId, "Paid", 50_000, now.AddHours(-2), now.AddHours(-1), 50, subjectId, "NCB", "QR"),
            CreateOrder(userAId, "Paid", 100_000, now.AddDays(-10), now.AddDays(-10).AddMinutes(5), 100, null, "NCB", "ATM"),
            CreateOrder(userBId, "Paid", 200_000, now.AddDays(-1), now.AddDays(-1).AddMinutes(5), null, null, "VCB", "VISA", "{}"),
            CreateOrder(userBId, "Failed", 100_000, now.AddHours(-3), null, 100, null, null, null),
            CreateOrder(userAId, "Pending", 50_000, now.AddHours(-1), null, 50, null, null, null),
            CreateOrder(userBId, "Pending", 50_000, now.AddMinutes(-5), null, 50, null, null, null));

        await dbContext.SaveChangesAsync();

        var service = new BillingReportQueryService(dbContext);
        var result = await service.GetBillingReportAsync();

        Assert.Equal(6, result.TotalOrders);
        Assert.Equal(3, result.PaidOrders);
        Assert.Equal(2, result.PendingOrders);
        Assert.Equal(1, result.FailedOrders);
        Assert.Equal(1, result.StalePendingOrders);
        Assert.Equal(350_000, result.GrossPaidRevenueVnd);
        Assert.Equal(3, result.PaidOrdersLast30Days);
        Assert.Equal(350_000, result.PaidRevenueLast30DaysVnd);
        Assert.Equal(116_666.67, result.AveragePaidOrderValueVnd);
        Assert.Equal(75, result.SettledPaymentSuccessPercent);
        Assert.Equal(60, result.CheckoutCompletionPercent);

        Assert.Equal(2, result.UniquePayingUsers);
        Assert.Equal(150, result.PurchasedQuotaUnits);
        Assert.Equal(75, result.AverageQuotaUnitsPerPaidOrder);
        Assert.Equal(1_000, result.EffectiveRevenuePerQuotaUnitVnd);
        Assert.Equal(1, result.PaidOrdersMissingQuotaMetadata);
        Assert.Equal(1, result.SubjectAttributedPaidOrders);
        Assert.Equal(2, result.UnattributedPaidOrders);

        Assert.Equal(2, result.RegisteredUsers);
        Assert.Equal(1, result.UsersWithAvailableQuota);
        Assert.Equal(7, result.TotalOutstandingQuota);

        Assert.Equal(2, result.QuotaPackageMix.Count);
        Assert.Contains(result.QuotaPackageMix, item => item.QuotaUnits == 50 && item.PaidOrders == 1);
        Assert.Contains(result.QuotaPackageMix, item => item.QuotaUnits == 100 && item.PaidOrders == 1);

        var ncb = Assert.Single(result.PaymentsByBank, item => item.Label == "NCB");
        Assert.Equal(2, ncb.PaidOrders);
        Assert.Equal(150_000, ncb.PaidRevenueVnd);
        Assert.Equal(66.7, ncb.PaidOrderSharePercent);

        Assert.Equal(5, result.DailyActivityLast7Days.Sum(day => day.CreatedOrders));
        Assert.Equal(2, result.DailyActivityLast7Days.Sum(day => day.PaidOrders));
        Assert.Equal(250_000, result.DailyActivityLast7Days.Sum(day => day.PaidRevenueVnd));
        Assert.Equal(50, result.DailyActivityLast7Days.Sum(day => day.PurchasedQuotaUnits));

        Assert.Equal(6, result.RecentOrders.Count);
        Assert.Contains(result.RecentOrders, order =>
            order.Status == "Paid" && order.IsSubjectAttributed && order.QuotaUnits == 50);
    }

    private static ApplicationUser CreateUser(Guid id, string userName, int quotaRemaining)
    {
        return new ApplicationUser
        {
            Id = id,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            DisplayName = userName,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-30),
            QuotaRemaining = quotaRemaining
        };
    }

    private static PaymentOrder CreateOrder(
        Guid userId,
        string status,
        long amount,
        DateTime createdUtc,
        DateTime? paidUtc,
        int? quotaUnits,
        Guid? subjectId,
        string? bankCode,
        string? cardType,
        string? metadataOverride = null)
    {
        return new PaymentOrder
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SubjectId = subjectId,
            Provider = "VNPay",
            ExternalOrderId = $"TEST-{Guid.NewGuid():N}",
            Amount = amount,
            Currency = "VND",
            Status = status,
            CreatedUtc = createdUtc,
            PaidUtc = paidUtc,
            BankCode = bankCode,
            CardType = cardType,
            MetadataJson = metadataOverride ?? $"{{\"quotaUnits\":{quotaUnits ?? 0}}}"
        };
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"billing-report-tests-{Guid.NewGuid()}")
            .ReplaceService<IModelCustomizer, BillingReportTestModelCustomizer>()
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed class BillingReportTestModelCustomizer : ModelCustomizer
    {
        public BillingReportTestModelCustomizer(ModelCustomizerDependencies dependencies)
            : base(dependencies)
        {
        }

        public override void Customize(ModelBuilder modelBuilder, DbContext context)
        {
            base.Customize(modelBuilder, context);
            modelBuilder.Entity<DocumentChunk>().Ignore(chunk => chunk.Embedding);
        }
    }
}
