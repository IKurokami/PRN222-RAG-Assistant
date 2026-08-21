using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PRN222.RagAssistant.Application.Models;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Infrastructure.Billing;
using Xunit;

namespace PRN222.RagAssistant.Tests;

public sealed class VnPayBillingServiceTests
{
    private const string TestHashSecret = "EUKFJAYATVRRJBDQHUWZTJRCTCSSOYVY";
    private const string TestTmnCode = "8OTYXKKM";
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

        var service = new VnPayBillingService(
            dbContext,
            billingOptions,
            NullLogger<VnPayBillingService>.Instance);

        return (dbContext, service);
    }

    [Fact]
    public async Task CreateOrderAsync_PersistsOrderInPendingStatus_AndReturnsCheckoutUrl()
    {
        // Arrange
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

        // Act
        var result = await service.CreateOrderAsync(request, CancellationToken.None);

        // Assert
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

        // Check query parameters in generated URL
        var uri = result.CheckoutUrl;
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        Assert.Equal(TestTmnCode, query["vnp_TmnCode"]);
        Assert.Equal("5000000", query["vnp_Amount"]); // 50,000 * 100
        Assert.Equal("VND", query["vnp_CurrCode"]);
        Assert.Equal(result.ExternalOrderId, query["vnp_TxnRef"]);
        Assert.False(string.IsNullOrWhiteSpace(query["vnp_SecureHash"]));
    }

    [Fact]
    public async Task EndToEnd_LiveSandboxCredentials_Checkout_Return_And_Webhook_Flow()
    {
        // Arrange - using the user's live sandbox credentials
        var (dbContext, service) = CreateService(TestTmnCode, TestHashSecret);
        var userId = Guid.NewGuid();

        // 1. User initiates payment for 100,000 VND (100 RAG queries)
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

        // 2. Simulate user completing payment on VNPay sandbox (NCB Test Card)
        // VNPay redirects browser back to ReturnUrl with valid HMAC-SHA512
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

        // Compute valid signature using the secret key
        var returnHash = VnPayHashHelper.CreateSecureHash(returnParams, TestHashSecret);
        returnParams["vnp_SecureHash"] = returnHash;

        // Process Return
        var returnStatus = await service.ProcessReturnAsync(
            new ProcessReturnRequest(createResult.OrderId, returnParams),
            CancellationToken.None);

        Assert.Equal("Paid", returnStatus.Status);
        Assert.Equal("14567890", returnStatus.ExternalTransactionNo);
        Assert.Equal("NCB", returnStatus.BankCode);
        Assert.Equal("ATM", returnStatus.CardType);
        Assert.NotNull(returnStatus.PaidUtc);

        // 3. Simulate asynchronous IPN Webhook call from VNPay server
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
        Assert.Equal("Already processed", webhookResult.Message); // Idempotent because return already marked it Paid
        // 4. Verify persisted state in Database
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
        // Arrange
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

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ProcessReturnAsync(
                new ProcessReturnRequest(createResult.OrderId, callbackParams),
                CancellationToken.None));
    }

    [Fact]
    public async Task ProcessWebhookAsync_ValidSuccessfulPayment_MarksOrderPaid_AndReturnsConfirmSuccess()
    {
        // Arrange
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

        // Act
        var webhookResult = await service.ProcessWebhookAsync(
            new ProcessWebhookRequest("VNPay", webhookParams),
            CancellationToken.None);

        // Assert
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
        // Arrange
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

        // First call
        var firstResult = await service.ProcessWebhookAsync(
            new ProcessWebhookRequest("VNPay", webhookParams),
            CancellationToken.None);
        Assert.True(firstResult.Success);

        // Second duplicate call from VNPay retry
        var secondResult = await service.ProcessWebhookAsync(
            new ProcessWebhookRequest("VNPay", webhookParams),
            CancellationToken.None);

        // Assert
        Assert.True(secondResult.Success);
        Assert.Equal("Already processed", secondResult.Message);
    }

    [Fact]
    public async Task ProcessWebhookAsync_AmountMismatch_RejectsWebhook()
    {
        // Arrange
        var (dbContext, service) = CreateService();
        var createResult = await service.CreateOrderAsync(
            new CreateBillingOrderRequest(
                Guid.NewGuid(), null, 50_000, "VND", "Nạp 50 câu",
                new Uri("https://localhost:7001/Billing/Return"), "127.0.0.1"),
            CancellationToken.None);

        // Webhook sends wrong amount (e.g. 10,000 * 100 instead of 50,000 * 100)
        var webhookParams = new Dictionary<string, string?>
        {
            ["vnp_Amount"] = "1000000",
            ["vnp_ResponseCode"] = "00",
            ["vnp_TransactionNo"] = "11223344",
            ["vnp_TxnRef"] = createResult.ExternalOrderId
        };

        var hash = VnPayHashHelper.CreateSecureHash(webhookParams, TestHashSecret);
        webhookParams["vnp_SecureHash"] = hash;

        // Act
        var result = await service.ProcessWebhookAsync(
            new ProcessWebhookRequest("VNPay", webhookParams),
            CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Amount mismatch", result.Message);

        var orderInDb = await dbContext.PaymentOrders.FindAsync(createResult.OrderId);
        Assert.NotNull(orderInDb);
        Assert.Equal("Pending", orderInDb.Status); // Should remain Pending
    }

    [Fact]
    public async Task GetUserOrdersAsync_ReturnsOrdersSortedByCreatedUtc()
    {
        // Arrange
        var (dbContext, service) = CreateService();
        var userId = Guid.NewGuid();

        await service.CreateOrderAsync(
            new CreateBillingOrderRequest(userId, null, 50_000, "VND", "Order 1", new Uri("https://localhost:7001/Billing/Return"), "127.0.0.1"),
            CancellationToken.None);

        await service.CreateOrderAsync(
            new CreateBillingOrderRequest(userId, null, 100_000, "VND", "Order 2", new Uri("https://localhost:7001/Billing/Return"), "127.0.0.1"),
            CancellationToken.None);

        // Act
        var userOrders = await service.GetUserOrdersAsync(userId, CancellationToken.None);

        // Assert
        Assert.Equal(2, userOrders.Count);
        Assert.Equal(100_000, userOrders[0].Amount); // Newest first
        Assert.Equal(50_000, userOrders[1].Amount);
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
