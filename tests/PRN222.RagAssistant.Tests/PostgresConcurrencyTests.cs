using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pgvector.EntityFrameworkCore;
using PRN222.RagAssistant.Application.Models;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Infrastructure.Billing;
using PRN222.RagAssistant.Infrastructure.Rag.Exceptions;
using PRN222.RagAssistant.Infrastructure.Services;

namespace PRN222.RagAssistant.Tests;

public sealed class PostgresConcurrencyTests
{
    private const string TestHashSecret = "postgres-concurrency-test-secret";
    private const string TestTmnCode = "TESTTMN1";

    [Fact]
    public async Task ConcurrentQuotaReservations_WithOneRemaining_AllowExactlyOneRequest()
    {
        var connectionString = GetConnectionStringOrSkip();
        if (connectionString is null) return;

        var userId = Guid.NewGuid();
        await using (var setup = CreateContext(connectionString))
        {
            setup.Users.Add(NewUser(userId, 1));
            await setup.SaveChangesAsync();
            await SetQuotaAsync(setup, userId, 1);
        }

        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<bool> AttemptReservationAsync()
        {
            await using var context = CreateContext(connectionString);
            var service = new UserQuotaService(context);
            await start.Task;
            try
            {
                await using var reservation = await service.ReserveQuotaAsync(userId);
                reservation.Activate();
                await Task.Delay(150);
                await service.ConsumeQuotaAsync(userId);
                return true;
            }
            catch (InsufficientQuotaException)
            {
                return false;
            }
        }

        var first = AttemptReservationAsync();
        var second = AttemptReservationAsync();
        start.SetResult();
        var results = await Task.WhenAll(first, second);

        Assert.Single(results, result => result);
        await using var verify = CreateContext(connectionString);
        Assert.Equal(0, await verify.Users.Where(u => u.Id == userId).Select(u => u.QuotaRemaining).SingleAsync());
    }

    [Fact]
    public async Task ConcurrentSuccessfulIpnCallbacks_CreditQuotaExactlyOnce()
    {
        var connectionString = GetConnectionStringOrSkip();
        if (connectionString is null) return;

        var userId = Guid.NewGuid();
        BillingOrderResult orderResult;
        await using (var setup = CreateContext(connectionString))
        {
            setup.Users.Add(NewUser(userId, 0));
            await setup.SaveChangesAsync();
            await SetQuotaAsync(setup, userId, 0);
            orderResult = await CreateBillingService(setup).CreateOrderAsync(
                new CreateBillingOrderRequest(
                    userId,
                    null,
                    50_000,
                    "VND",
                    "Nap 37 cau hoi",
                    new Uri("https://merchant.test/Billing/Return"),
                    "127.0.0.1",
                    37),
                CancellationToken.None);
        }

        var callback = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["vnp_TmnCode"] = TestTmnCode,
            ["vnp_TxnRef"] = orderResult.ExternalOrderId,
            ["vnp_Amount"] = "5000000",
            ["vnp_ResponseCode"] = "00",
            ["vnp_TransactionStatus"] = "00",
            ["vnp_TransactionNo"] = "99887766",
            ["vnp_BankCode"] = "NCB",
            ["vnp_CardType"] = "ATM",
            ["vnp_PayDate"] = "20260822080000"
        };
        callback["vnp_SecureHash"] = VnPayHashHelper.CreateSecureHash(callback, TestHashSecret);

        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<BillingWebhookResult> SendIpnAsync()
        {
            await using var context = CreateContext(connectionString);
            var service = CreateBillingService(context);
            await start.Task;
            return await service.ProcessWebhookAsync(
                new ProcessWebhookRequest("VNPay", callback),
                CancellationToken.None);
        }

        var first = SendIpnAsync();
        var second = SendIpnAsync();
        start.SetResult();
        var results = await Task.WhenAll(first, second);

        Assert.Equal(new[] { "00", "02" }, results.Select(r => r.ResponseCode).OrderBy(code => code).ToArray());

        await using var verify = CreateContext(connectionString);
        var userQuota = await verify.Users.Where(u => u.Id == userId).Select(u => u.QuotaRemaining).SingleAsync();
        var order = await verify.PaymentOrders.AsNoTracking().SingleAsync(o => o.Id == orderResult.OrderId);
        Assert.Equal(37, userQuota);
        Assert.Equal("Paid", order.Status);
    }

    private static string? GetConnectionStringOrSkip() =>
        Environment.GetEnvironmentVariable("POSTGRES_TEST_CONNECTION");

    private static ApplicationDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.UseVector())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task SetQuotaAsync(ApplicationDbContext context, Guid userId, int quota) =>
        _ = await context.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.QuotaRemaining, quota));

    private static ApplicationUser NewUser(Guid id, int quota) =>
        new()
        {
            Id = id,
            UserName = $"concurrency-{id:N}@test.local",
            Email = $"concurrency-{id:N}@test.local",
            DisplayName = "Concurrency Test User",
            CreatedAtUtc = DateTime.UtcNow,
            QuotaRemaining = quota
        };

    private static VnPayBillingService CreateBillingService(ApplicationDbContext context) =>
        new(
            context,
            Options.Create(new BillingOptions { Enabled = true, BaseUrl = "https://merchant.test" }),
            Options.Create(new VnPayBillingOptions
            {
                TmnCode = TestTmnCode,
                HashSecret = TestHashSecret,
                BaseUrl = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html"
            }),
            NullLogger<VnPayBillingService>.Instance,
            new UserQuotaService(context));
}
