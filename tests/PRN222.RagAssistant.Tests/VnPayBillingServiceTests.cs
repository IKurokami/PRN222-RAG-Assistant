using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PRN222.RagAssistant.Application.Models;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Infrastructure.Billing;
using PRN222.RagAssistant.Infrastructure.Services;
using Xunit;

namespace PRN222.RagAssistant.Tests;

public sealed class VnPayBillingServiceTests
{
    private const string TestHashSecret = "test-vnpay-hash-secret-for-unit-tests-only";
    private const string TestTmnCode = "TESTTMN001";
    private const string TestBaseUrl = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";

    private static (ApplicationDbContext DbContext, VnPayBillingService Service) CreateService(
        string tmnCode = TestTmnCode,
        string hashSecret = TestHashSecret)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ReplaceService<IModelCustomizer, TestModelCustomizer>()
            .Options;

        var dbContext = new ApplicationDbContext(options);

        var billingOptions = Options.Create(new VnPayBillingOptions
        {
            TmnCode = tmnCode,
            HashSecret = hashSecret,
            BaseUrl = TestBaseUrl,
            Version = "2.1.0",
            Command = "pay",
            Locale = "vn",
            OrderType = "other"
        });

        var quotaService = new UserQuotaService(dbContext);

        var service = new VnPayBillingService(
            dbContext,
            billingOptions,
            NullLogger<VnPayBillingService>.Instance,
            quotaService);
        return (dbContext, service);
    }

    [Fact]
    public async Task CreateOrderAsync_PersistsOrderInPendingStatus_AndReturnsCheckoutUrl()
    {
        var (dbContext, service) = CreateService();
        var userId = Guid.NewGuid();
        var request = new CreateBillingOrderRequest(
            userId,
            null,
            50_000,
            "VND",
            "Nạp 50 lượt hỏi",
            new Uri("https://localhost:7001/Billing/Return"),
            "127.0.0.1");

        var result = await service.CreateOrderAsync(request, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.OrderId);
        Assert.StartsWith("PRN222-", result.ExternalOrderId);
        Assert.NotNull(result.CheckoutUrl);

        var persisted = await dbContext.PaymentOrders.FindAsync(result.OrderId);
        Assert.NotNull(persisted);
        Assert.Equal(userId, persisted.UserId);
        Assert.Equal("Pending", persisted.Status);
        Assert.Equal(50_000, persisted.Amount);
        Assert.Equal("VND", persisted.Currency);
        Assert.Equal("VNPay", persisted.Provider);

        var uri = result.CheckoutUrl;
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        Assert.Equal(TestTmnCode, query["vnp_TmnCode"]);
        Assert.Equal("5000000", query["vnp_Amount"]);
        Assert.Equal("VND", query["vnp_CurrCode"]);
        Assert.Equal(result.ExternalOrderId, query["vnp_TxnRef"]);
        Assert.False(string.IsNullOrWhiteSpace(query["vnp_SecureHash"]));
    }

    [Fact]
    public async Task EndToEnd_SimulatedSandbox_Checkout_Return_And_Webhook_Flow()
    {
        var (dbContext, service) = CreateService(TestTmnCode, TestHashSecret);
        var userId = Guid.NewGuid();

        var createRequest = new CreateBillingOrderRequest(
            userId,
            null,
            100_000,
            "VND",
            "Nạp 100 câu hỏi RAG",
            new Uri("https://localhost:7001/Billing/Return"),
            "127.0.0.1");

        var createResult = await service.CreateOrderAsync(createRequest, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, createResult.OrderId);
        Assert.NotNull(createResult.CheckoutUrl);
        Assert.Contains($"vnp_TmnCode={TestTmnCode}", createResult.CheckoutUrl.ToString());

        var returnParams = new Dictionary<string, string?>
        {
            ["vnp_Amount"] = "10000000",
            ["vnp_BankCode"] = "NCB",
            ["vnp_BankTranNo"] = "VNP14567890",
            ["vnp_CardType"] = "ATM",
            ["vnp_OrderInfo"] = "Nạp 100 câu hỏi RAG",
            ["vnp_PayDate"] = "20260821171500",
            ["vnp_ResponseCode"] = "00",
            ["vnp_TmnCode"] = TestTmnCode,
            ["vnp_TransactionNo"] = "14567890",
            ["vnp_TransactionStatus"] = "00",
            ["vnp_TxnRef"] = createResult.ExternalOrderId
        };

        var returnHash = VnPayHashHelper.CreateSecureHash(returnParams, TestHashSecret);
        returnParams["vnp_SecureHash"] = returnHash;

        var returnStatus = await service.ProcessReturnAsync(
            new ProcessReturnRequest(createResult.OrderId, returnParams),
            CancellationToken.None);

        Assert.Equal("Paid", returnStatus.Status);
        Assert.Equal("14567890", returnStatus.ExternalTransactionNo);
        Assert.Equal("NCB", returnStatus.BankCode);
        Assert.Equal("ATM", returnStatus.CardType);
        Assert.NotNull(returnStatus.PaidUtc);

        var webhookParams = new Dictionary<string, string?>
        {
            ["vnp_Amount"] = "10000000",
            ["vnp_BankCode"] = "NCB",
            ["vnp_CardType"] = "ATM",
            ["vnp_OrderInfo"] = "Nạp 100 câu hỏi RAG",
            ["vnp_PayDate"] = "20260821171500",
            ["vnp_ResponseCode"] = "00",
            ["vnp_TmnCode"] = TestTmnCode,
            ["vnp_TransactionNo"] = "14567890",
            ["vnp_TransactionStatus"] = "00",
            ["vnp_TxnRef"] = createResult.ExternalOrderId
        };

        var webhookHash = VnPayHashHelper.CreateSecureHash(webhookParams, TestHashSecret);
        webhookParams["vnp_SecureHash"] = webhookHash;

        var webhookResult = await service.ProcessWebhookAsync(
            new ProcessWebhookRequest("VNPay", webhookParams),
            CancellationToken.None);

        Assert.True(webhookResult.Success);
        Assert.Equal("Already processed", webhookResult.Message);

        var finalOrder = await dbContext.PaymentOrders.FindAsync(createResult.OrderId);
        Assert.NotNull(finalOrder);
        Assert.Equal("Paid", finalOrder.Status);
        Assert.Equal(100_000, finalOrder.Amount);
        Assert.Equal("14567890", finalOrder.ExternalTransactionNo);
        Assert.Equal("NCB", finalOrder.BankCode);
    }

    [Fact]
    public async Task ProcessReturnAsync_InvalidHash_ThrowsInvalidOperationException()
    {
        var (dbContext, service) = CreateService();
        var createResult = await service.CreateOrderAsync(
            new CreateBillingOrderRequest(
                Guid.NewGuid(), null, 50_000, "VND", "Nạp 50 câu",
                new Uri("https://localhost:7001/Billing/Return"), "127.0.0.1"),
            CancellationToken.None);

        var callbackParams = new Dictionary<string, string?>
        {
            ["vnp_Amount"] = "5000000",
            ["vnp_ResponseCode"] = "00",
            ["vnp_TxnRef"] = createResult.ExternalOrderId,
            ["vnp_SecureHash"] = "INVALID_HASH_VALUE_12345"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ProcessReturnAsync(
                new ProcessReturnRequest(createResult.OrderId, callbackParams),
                CancellationToken.None));
    }

    [Fact]
    public async Task ProcessWebhookAsync_ValidSuccessfulPayment_MarksOrderPaid_AndReturnsConfirmSuccess()
    {
        var (dbContext, service) = CreateService();
        var createResult = await service.CreateOrderAsync(
            new CreateBillingOrderRequest(
                Guid.NewGuid(), null, 200_000, "VND", "Nạp 200 câu",
                new Uri("https://localhost:7001/Billing/Return"), "127.0.0.1"),
            CancellationToken.None);

        var webhookParams = new Dictionary<string, string?>
        {
            ["vnp_Amount"] = "20000000",
            ["vnp_BankCode"] = "VIETCOMBANK",
            ["vnp_CardType"] = "QRCODE",
            ["vnp_ResponseCode"] = "00",
            ["vnp_TransactionNo"] = "99887766",
            ["vnp_TransactionStatus"] = "00",
            ["vnp_TxnRef"] = createResult.ExternalOrderId
        };

        var hash = VnPayHashHelper.CreateSecureHash(webhookParams, TestHashSecret);
        webhookParams["vnp_SecureHash"] = hash;

        var webhookResult = await service.ProcessWebhookAsync(
            new ProcessWebhookRequest("VNPay", webhookParams),
            CancellationToken.None);

        Assert.True(webhookResult.Success);
        Assert.Equal("Confirm Success", webhookResult.Message);

        var orderInDb = await dbContext.PaymentOrders.FindAsync(createResult.OrderId);
        Assert.NotNull(orderInDb);
        Assert.Equal("Paid", orderInDb.Status);
        Assert.Equal("99887766", orderInDb.ExternalTransactionNo);
        Assert.Equal("VIETCOMBANK", orderInDb.BankCode);
    }

    [Fact]
    public async Task ProcessWebhookAsync_RepeatedCall_IsIdempotent()
    {
        var (dbContext, service) = CreateService();
        var createResult = await service.CreateOrderAsync(
            new CreateBillingOrderRequest(
                Guid.NewGuid(), null, 50_000, "VND", "Nạp 50 câu",
                new Uri("https://localhost:7001/Billing/Return"), "127.0.0.1"),
            CancellationToken.None);

        var webhookParams = new Dictionary<string, string?>
        {
            ["vnp_Amount"] = "5000000",
            ["vnp_ResponseCode"] = "00",
            ["vnp_TransactionNo"] = "11223344",
            ["vnp_TxnRef"] = createResult.ExternalOrderId
        };

        var hash = VnPayHashHelper.CreateSecureHash(webhookParams, TestHashSecret);
        webhookParams["vnp_SecureHash"] = hash;

        var firstResult = await service.ProcessWebhookAsync(
            new ProcessWebhookRequest("VNPay", webhookParams),
            CancellationToken.None);
        Assert.True(firstResult.Success);

        var secondResult = await service.ProcessWebhookAsync(
            new ProcessWebhookRequest("VNPay", webhookParams),
            CancellationToken.None);

        Assert.True(secondResult.Success);
        Assert.Equal("Already processed", secondResult.Message);
    }

    [Fact]
    public async Task ProcessWebhookAsync_AmountMismatch_RejectsWebhook()
    {
        var (dbContext, service) = CreateService();
        var createResult = await service.CreateOrderAsync(
            new CreateBillingOrderRequest(
                Guid.NewGuid(), null, 50_000, "VND", "Nạp 50 câu",
                new Uri("https://localhost:7001/Billing/Return"), "127.0.0.1"),
            CancellationToken.None);

        var webhookParams = new Dictionary<string, string?>
        {
            ["vnp_Amount"] = "1000000",
            ["vnp_ResponseCode"] = "00",
            ["vnp_TransactionNo"] = "11223344",
            ["vnp_TxnRef"] = createResult.ExternalOrderId
        };

        var hash = VnPayHashHelper.CreateSecureHash(webhookParams, TestHashSecret);
        webhookParams["vnp_SecureHash"] = hash;

        var result = await service.ProcessWebhookAsync(
            new ProcessWebhookRequest("VNPay", webhookParams),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Amount mismatch", result.Message);

        var orderInDb = await dbContext.PaymentOrders.FindAsync(createResult.OrderId);
        Assert.NotNull(orderInDb);
        Assert.Equal("Pending", orderInDb.Status);
    }

    [Fact]
    public async Task GetUserOrdersAsync_ReturnsOrdersSortedByCreatedUtc()
    {
        var (dbContext, service) = CreateService();
        var userId = Guid.NewGuid();

        await service.CreateOrderAsync(
            new CreateBillingOrderRequest(userId, null, 50_000, "VND", "Order 1", new Uri("https://localhost:7001/Billing/Return"), "127.0.0.1"),
            CancellationToken.None);

        await service.CreateOrderAsync(
            new CreateBillingOrderRequest(userId, null, 100_000, "VND", "Order 2", new Uri("https://localhost:7001/Billing/Return"), "127.0.0.1"),
            CancellationToken.None);

        var userOrders = await service.GetUserOrdersAsync(userId, CancellationToken.None);

        Assert.Equal(2, userOrders.Count);
        Assert.Equal(100_000, userOrders[0].Amount);
        Assert.Equal(50_000, userOrders[1].Amount);
    }

    [Fact]
    public async Task ProcessWebhookAsync_GrantsQuotaToUser_WhenPaymentSuccessful()
    {
        var (dbContext, service) = CreateService();
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "student@test.com",
            Email = "student@test.com",
            DisplayName = "Test Student",
            CreatedAtUtc = DateTime.UtcNow,
            QuotaRemaining = 10
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var order = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Provider = "VNPay",
            ExternalOrderId = "PRN222-20260821-QUOTA001",
            Amount = 50_000,
            Currency = "VND",
            Status = "Pending",
            CreatedUtc = DateTime.UtcNow,
            MetadataJson = "{}"
        };
        dbContext.PaymentOrders.Add(order);
        await dbContext.SaveChangesAsync();

        var callbackParams = new Dictionary<string, string?>
        {
            ["vnp_TxnRef"] = order.ExternalOrderId,
            ["vnp_Amount"] = "5000000",
            ["vnp_ResponseCode"] = "00",
            ["vnp_TransactionNo"] = "14000111",
            ["vnp_BankCode"] = "NCB",
            ["vnp_CardType"] = "ATM"
        };
        var hash = VnPayHashHelper.CreateSecureHash(callbackParams, TestHashSecret);
        callbackParams["vnp_SecureHash"] = hash;

        var request = new ProcessWebhookRequest("VNPay", callbackParams);

        var result = await service.ProcessWebhookAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        var updatedUser = await dbContext.Users.FirstAsync(u => u.Id == userId);
        Assert.Equal(60, updatedUser.QuotaRemaining);
    }

    private sealed class TestModelCustomizer : ModelCustomizer
    {
        public TestModelCustomizer(ModelCustomizerDependencies dependencies)
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
