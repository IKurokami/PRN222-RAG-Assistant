using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PRN222.RagAssistant.Application.Models;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Infrastructure.Billing;
using PRN222.RagAssistant.Infrastructure.Services;

namespace PRN222.RagAssistant.Tests;

public sealed class VnPayBillingServiceTests
{
    private const string TestHashSecret = "test-vnpay-hash-secret-for-unit-tests-only";
    private const string TestTmnCode = "TESTTMN1";
    private const string TestBaseUrl = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";

    [Fact]
    public async Task CreateOrderAsync_PersistsImmutableQuotaUnits_AndBuildsSpecCompliantUrl()
    {
        var (dbContext, service, userId) = await CreateServiceAsync(initialQuota: 10);
        await using var _ = dbContext;

        var result = await service.CreateOrderAsync(
            NewOrderRequest(userId, amount: 50_000, quotaUnits: 77),
            CancellationToken.None);

        var order = await dbContext.PaymentOrders.FindAsync(result.OrderId);
        Assert.NotNull(order);
        Assert.Equal("Pending", order.Status);

        using var metadata = JsonDocument.Parse(order.MetadataJson);
        Assert.Equal(77, metadata.RootElement.GetProperty("quotaUnits").GetInt32());

        var query = System.Web.HttpUtility.ParseQueryString(result.CheckoutUrl.Query);
        Assert.Equal("5000000", query["vnp_Amount"]);
        Assert.Equal(TestTmnCode, query["vnp_TmnCode"]);
        Assert.Matches("^[0-9]{14}$", query["vnp_CreateDate"] ?? string.Empty);
        Assert.Matches("^[0-9]{14}$", query["vnp_ExpireDate"] ?? string.Empty);
        Assert.False(string.IsNullOrWhiteSpace(query["vnp_SecureHash"]));
    }

    [Fact]
    public async Task ProcessReturnAsync_VerifiedSuccessfulReturn_FinalizesOrderAndQuotaAsFallback()
    {
        var (dbContext, service, userId) = await CreateServiceAsync(initialQuota: 10);
        await using var _ = dbContext;
        var result = await service.CreateOrderAsync(NewOrderRequest(userId, 50_000, 50), CancellationToken.None);
        var callback = BuildSuccessfulCallback(result.ExternalOrderId, 50_000);

        var displayStatus = await service.ProcessReturnAsync(
            new ProcessReturnRequest(result.OrderId, callback),
            CancellationToken.None);

        Assert.Equal("Paid", displayStatus.Status);
        var order = await dbContext.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == result.OrderId);
        var user = await dbContext.Users.AsNoTracking().FirstAsync(u => u.Id == userId);
        Assert.Equal("Paid", order.Status);
        Assert.Equal(60, user.QuotaRemaining);
        Assert.Equal("14567890", order.ExternalTransactionNo);
    }

    [Fact]
    public async Task ProcessReturnAsync_ThenIpn_DoesNotDoubleCredit()
    {
        var (dbContext, service, userId) = await CreateServiceAsync(initialQuota: 0);
        await using var _ = dbContext;
        var result = await service.CreateOrderAsync(NewOrderRequest(userId, 50_000, 50), CancellationToken.None);
        var callback = BuildSuccessfulCallback(result.ExternalOrderId, 50_000);

        var returnStatus = await service.ProcessReturnAsync(
            new ProcessReturnRequest(result.OrderId, callback),
            CancellationToken.None);
        var webhookResult = await service.ProcessWebhookAsync(
            new ProcessWebhookRequest("VNPay", callback),
            CancellationToken.None);

        Assert.Equal("Paid", returnStatus.Status);
        Assert.Equal("02", webhookResult.ResponseCode);
        var user = await dbContext.Users.AsNoTracking().FirstAsync(u => u.Id == userId);
        Assert.Equal(50, user.QuotaRemaining);
    }

    [Fact]
    public async Task ProcessWebhookAsync_GrantsStoredQuotaUnits_NotAmountDerivedQuota()
    {
        var (dbContext, service, userId) = await CreateServiceAsync(initialQuota: 10);
        await using var _ = dbContext;
        var result = await service.CreateOrderAsync(NewOrderRequest(userId, 50_000, 77), CancellationToken.None);
        var callback = BuildSuccessfulCallback(result.ExternalOrderId, 50_000);

        var webhookResult = await service.ProcessWebhookAsync(
            new ProcessWebhookRequest("VNPay", callback),
            CancellationToken.None);

        Assert.True(webhookResult.Success);
        Assert.Equal("00", webhookResult.ResponseCode);
        var user = await dbContext.Users.AsNoTracking().FirstAsync(u => u.Id == userId);
        var order = await dbContext.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == result.OrderId);
        Assert.Equal(87, user.QuotaRemaining);
        Assert.Equal("Paid", order.Status);
    }

    [Fact]
    public async Task ProcessWebhookAsync_DuplicateCall_Returns02_AndDoesNotDoubleCredit()
    {
        var (dbContext, service, userId) = await CreateServiceAsync(initialQuota: 0);
        await using var _ = dbContext;
        var result = await service.CreateOrderAsync(NewOrderRequest(userId, 50_000, 50), CancellationToken.None);
        var callback = BuildSuccessfulCallback(result.ExternalOrderId, 50_000);

        var first = await service.ProcessWebhookAsync(new ProcessWebhookRequest("VNPay", callback), CancellationToken.None);
        var second = await service.ProcessWebhookAsync(new ProcessWebhookRequest("VNPay", callback), CancellationToken.None);

        Assert.Equal("00", first.ResponseCode);
        Assert.Equal("02", second.ResponseCode);
        var user = await dbContext.Users.AsNoTracking().FirstAsync(u => u.Id == userId);
        Assert.Equal(50, user.QuotaRemaining);
    }

    [Fact]
    public async Task ProcessWebhookAsync_AmountMismatch_Returns04()
    {
        var (dbContext, service, userId) = await CreateServiceAsync(initialQuota: 0);
        await using var _ = dbContext;
        var result = await service.CreateOrderAsync(NewOrderRequest(userId, 50_000, 50), CancellationToken.None);
        var callback = BuildSuccessfulCallback(result.ExternalOrderId, 10_000);

        var webhookResult = await service.ProcessWebhookAsync(
            new ProcessWebhookRequest("VNPay", callback),
            CancellationToken.None);

        Assert.False(webhookResult.Success);
        Assert.Equal("04", webhookResult.ResponseCode);
        var order = await dbContext.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == result.OrderId);
        Assert.Equal("Pending", order.Status);
    }

    [Fact]
    public async Task ProcessWebhookAsync_InvalidSignature_Returns97()
    {
        var (dbContext, service, userId) = await CreateServiceAsync(initialQuota: 0);
        await using var _ = dbContext;
        var result = await service.CreateOrderAsync(NewOrderRequest(userId, 50_000, 50), CancellationToken.None);
        var callback = BuildSuccessfulCallback(result.ExternalOrderId, 50_000);
        callback["vnp_SecureHash"] = "deadbeef";

        var webhookResult = await service.ProcessWebhookAsync(
            new ProcessWebhookRequest("VNPay", callback),
            CancellationToken.None);

        Assert.False(webhookResult.Success);
        Assert.Equal("97", webhookResult.ResponseCode);
    }

    private static async Task<(ApplicationDbContext DbContext, VnPayBillingService Service, Guid UserId)> CreateServiceAsync(int initialQuota)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ReplaceService<IModelCustomizer, TestModelCustomizer>()
            .Options;
        var dbContext = new ApplicationDbContext(options);
        var userId = Guid.NewGuid();
        dbContext.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = $"user-{userId:N}@test.local",
            Email = $"user-{userId:N}@test.local",
            DisplayName = "Test User",
            CreatedAtUtc = DateTime.UtcNow,
            QuotaRemaining = initialQuota
        });
        await dbContext.SaveChangesAsync();

        var service = new VnPayBillingService(
            dbContext,
            Options.Create(new BillingOptions { Enabled = true, BaseUrl = "https://merchant.test" }),
            Options.Create(new VnPayBillingOptions
            {
                TmnCode = TestTmnCode,
                HashSecret = TestHashSecret,
                BaseUrl = TestBaseUrl
            }),
            NullLogger<VnPayBillingService>.Instance,
            new UserQuotaService(dbContext));

        return (dbContext, service, userId);
    }

    private static CreateBillingOrderRequest NewOrderRequest(Guid userId, long amount, int quotaUnits) =>
        new(
            userId,
            null,
            amount,
            "VND",
            $"Nap {quotaUnits} cau hoi",
            new Uri("https://merchant.test/Billing/Return"),
            "127.0.0.1",
            quotaUnits);

    private static Dictionary<string, string?> BuildSuccessfulCallback(string externalOrderId, long amount)
    {
        var callback = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["vnp_TmnCode"] = TestTmnCode,
            ["vnp_TxnRef"] = externalOrderId,
            ["vnp_Amount"] = checked(amount * 100).ToString(),
            ["vnp_ResponseCode"] = "00",
            ["vnp_TransactionStatus"] = "00",
            ["vnp_TransactionNo"] = "14567890",
            ["vnp_BankCode"] = "NCB",
            ["vnp_CardType"] = "ATM",
            ["vnp_PayDate"] = "20260822080000"
        };
        callback["vnp_SecureHash"] = VnPayHashHelper.CreateSecureHash(callback, TestHashSecret);
        return callback;
    }

    private sealed class TestModelCustomizer(ModelCustomizerDependencies dependencies) : ModelCustomizer(dependencies)
    {
        public override void Customize(ModelBuilder modelBuilder, DbContext context)
        {
            base.Customize(modelBuilder, context);
            modelBuilder.Entity<DocumentChunk>().Ignore(chunk => chunk.Embedding);
        }
    }
}
