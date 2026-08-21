using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Application.Models;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;

namespace PRN222.RagAssistant.Infrastructure.Billing;

public sealed class VnPayBillingService : IBillingService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IOptions<VnPayBillingOptions> _options;
    private readonly ILogger<VnPayBillingService> _logger;

    public VnPayBillingService(
        ApplicationDbContext dbContext,
        IOptions<VnPayBillingOptions> options,
        ILogger<VnPayBillingService> logger)
    {
        _dbContext = dbContext;
        _options = options;
        _logger = logger;
    }

    public async Task<BillingOrderResult> CreateOrderAsync(
        CreateBillingOrderRequest request,
        CancellationToken cancellationToken)
    {
        var externalOrderId = GenerateExternalOrderId();

        var order = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            SubjectId = request.SubjectId,
            Provider = "VNPay",
            ExternalOrderId = externalOrderId,
            Amount = request.Amount,
            Currency = request.Currency,
            Status = "Pending",
            CreatedUtc = DateTime.UtcNow,
            MetadataJson = "{}"
        };

        _dbContext.PaymentOrders.Add(order);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var checkoutUrl = BuildCheckoutUrl(order, request);
        _logger.LogInformation(
            "Created VNPay order {OrderId}, ExternalOrderId={ExternalOrderId}, Amount={Amount} {Currency}",
            order.Id, externalOrderId, request.Amount, request.Currency);

        return new BillingOrderResult(order.Id, externalOrderId, checkoutUrl);
    }

    public async Task<BillingOrderStatus?> GetOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var order = await _dbContext.PaymentOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        return order is null ? null : Map(order);
    }

    public async Task<IReadOnlyList<BillingOrderStatus>> GetUserOrdersAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var orders = await _dbContext.PaymentOrders
            .AsNoTracking()
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedUtc)
            .ToListAsync(cancellationToken);

        return orders.Select(Map).ToList();
    }

    public async Task<BillingOrderStatus> ProcessReturnAsync(
        ProcessReturnRequest request,
        CancellationToken cancellationToken)
    {
        var externalOrderId = GetCallbackValue(request.CallbackParameters, "vnp_TxnRef");
        var order = await _dbContext.PaymentOrders
            .FirstOrDefaultAsync(
                o => (request.OrderId != Guid.Empty && o.Id == request.OrderId) ||
                     (!string.IsNullOrEmpty(externalOrderId) && o.ExternalOrderId == externalOrderId),
                cancellationToken);

        if (order is null)
        {
            throw new InvalidOperationException($"Payment order '{request.OrderId}' / '{externalOrderId}' not found.");
        }

        if (!VnPayHashHelper.VerifySecureHash(request.CallbackParameters, _options.Value.HashSecret))
        {
            throw new InvalidOperationException("Invalid VNPay return signature.");
        }

        if (string.Equals(order.Status, "Paid", StringComparison.Ordinal))
        {
            return Map(order);
        }

        var responseCode = GetCallbackValue(request.CallbackParameters, "vnp_ResponseCode");
        if (string.Equals(responseCode, "00", StringComparison.Ordinal))
        {
            await MarkOrderPaidAsync(order, request.CallbackParameters, cancellationToken);
            return Map(order);
        }

        await MarkOrderFailedAsync(order, request.CallbackParameters, cancellationToken);
        return Map(order);
    }

    public async Task<BillingWebhookResult> ProcessWebhookAsync(
        ProcessWebhookRequest request,
        CancellationToken cancellationToken)
    {
        if (string.Equals(request.Provider, "VNPay", StringComparison.OrdinalIgnoreCase))
        {
            if (!VnPayHashHelper.VerifySecureHash(request.CallbackParameters, _options.Value.HashSecret))
            {
                _logger.LogWarning("Invalid VNPay webhook signature");
                return new BillingWebhookResult(false, "Invalid signature");
            }

            var externalOrderId = GetCallbackValue(request.CallbackParameters, "vnp_TxnRef");
            if (string.IsNullOrEmpty(externalOrderId))
            {
                return new BillingWebhookResult(false, "Missing order reference");
            }

            var order = await _dbContext.PaymentOrders
                .FirstOrDefaultAsync(o => o.ExternalOrderId == externalOrderId, cancellationToken);

            if (order is null)
            {
                _logger.LogWarning("Webhook received for unknown order {ExternalOrderId}", externalOrderId);
                return new BillingWebhookResult(false, "Order not found");
            }

            if (string.Equals(order.Status, "Paid", StringComparison.Ordinal))
            {
                _logger.LogDebug("Webhook idempotent for order {OrderId}", order.Id);
                return new BillingWebhookResult(true, "Already processed");
            }

            var amountText = GetCallbackValue(request.CallbackParameters, "vnp_Amount");
            if (!long.TryParse(amountText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var webhookAmount))
            {
                return new BillingWebhookResult(false, "Invalid amount");
            }

            var expectedVnpAmount = order.Amount * 100;
            if (webhookAmount != expectedVnpAmount && webhookAmount != order.Amount)
            {
                _logger.LogWarning(
                    "Webhook amount mismatch for order {OrderId}: expected {Expected}, got {Got}",
                    order.Id, expectedVnpAmount, webhookAmount);
                return new BillingWebhookResult(false, "Amount mismatch");
            }

            var responseCode = GetCallbackValue(request.CallbackParameters, "vnp_ResponseCode");
            if (string.Equals(responseCode, "00", StringComparison.Ordinal))
            {
                await MarkOrderPaidAsync(order, request.CallbackParameters, cancellationToken);
                _logger.LogInformation(
                    "Webhook marked order {OrderId} as Paid, ExternalOrderId={ExternalOrderId}, TransactionNo={TransactionNo}",
                    order.Id, externalOrderId, GetCallbackValue(request.CallbackParameters, "vnp_TransactionNo"));

                return new BillingWebhookResult(true, "Confirm Success");
            }

            await MarkOrderFailedAsync(order, request.CallbackParameters, cancellationToken);
            return new BillingWebhookResult(true, "Recorded failed payment");
        }

        return new BillingWebhookResult(false, "Unsupported provider");
    }

    private static string GetCallbackValue(
        IReadOnlyDictionary<string, string?> parameters,
        string key)
    {
        if (parameters.TryGetValue(key, out var value))
        {
            return value?.Trim() ?? string.Empty;
        }
        return string.Empty;
    }

    private async Task MarkOrderPaidAsync(
        PaymentOrder order,
        IReadOnlyDictionary<string, string?> callbackParameters,
        CancellationToken cancellationToken)
    {
        order.Status = "Paid";
        order.ExternalResponseCode = GetCallbackValue(callbackParameters, "vnp_ResponseCode");
        order.ExternalTransactionNo = GetCallbackValue(callbackParameters, "vnp_TransactionNo");
        order.BankCode = GetCallbackValue(callbackParameters, "vnp_BankCode");
        order.CardType = GetCallbackValue(callbackParameters, "vnp_CardType");
        order.PaidUtc = DateTime.UtcNow;
        order.ErrorMessage = null;

        order.MetadataJson = System.Text.Json.JsonSerializer.Serialize(
            new
            {
                amount = GetCallbackValue(callbackParameters, "vnp_Amount"),
                transactionNo = GetCallbackValue(callbackParameters, "vnp_TransactionNo"),
                bankCode = GetCallbackValue(callbackParameters, "vnp_BankCode"),
                cardType = GetCallbackValue(callbackParameters, "vnp_CardType"),
                responseCode = GetCallbackValue(callbackParameters, "vnp_ResponseCode"),
                transactionStatus = GetCallbackValue(callbackParameters, "vnp_TransactionStatus"),
                payDate = GetCallbackValue(callbackParameters, "vnp_PayDate")
            });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task MarkOrderFailedAsync(
        PaymentOrder order,
        IReadOnlyDictionary<string, string?> callbackParameters,
        CancellationToken cancellationToken)
    {
        order.Status = "Failed";
        order.ExternalResponseCode = GetCallbackValue(callbackParameters, "vnp_ResponseCode");
        order.ErrorMessage = $"VNPay response code: {order.ExternalResponseCode}";
        order.PaidUtc = null;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private Uri BuildCheckoutUrl(PaymentOrder order, CreateBillingOrderRequest request)
    {
        var options = _options.Value;
        var sanitizedOrderInfo = RemoveDiacritics(request.Description);
        if (string.IsNullOrWhiteSpace(sanitizedOrderInfo))
        {
            sanitizedOrderInfo = $"Nap quota PRN222 don {order.ExternalOrderId}";
        }

        var parameters = new Dictionary<string, string?>
        {
            ["vnp_Version"] = options.Version,
            ["vnp_Command"] = options.Command,
            ["vnp_TmnCode"] = options.TmnCode,
            ["vnp_Amount"] = (order.Amount * 100).ToString(CultureInfo.InvariantCulture),
            ["vnp_CurrCode"] = order.Currency,
            ["vnp_TxnRef"] = order.ExternalOrderId,
            ["vnp_OrderInfo"] = sanitizedOrderInfo,
            ["vnp_IpAddr"] = request.IpAddress,
            ["vnp_ReturnUrl"] = request.ReturnUrl.ToString(),
            ["vnp_CreateDate"] = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
            ["vnp_Locale"] = options.Locale,
            ["vnp_OrderType"] = options.OrderType
        };

        var url = VnPayHashHelper.BuildFullUrl(options.BaseUrl, parameters, options.HashSecret);
        return new Uri(url, UriKind.Absolute);
    }

    private static string RemoveDiacritics(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder(normalizedString.Length);

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        var cleaned = stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        // Keep only alphanumeric, space, hyphens
        var asciiOnly = new StringBuilder(cleaned.Length);
        foreach (var c in cleaned)
        {
            if (c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or ' ' or '-' or '_')
            {
                asciiOnly.Append(c);
            }
        }

        return asciiOnly.ToString().Trim();
    }

    private static string GenerateExternalOrderId()
    {
        var date = DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var guid = Guid.NewGuid().ToString("N")[..12];
        return $"PRN222-{date}-{guid}";
    }

    private static BillingOrderStatus Map(PaymentOrder order)
    {
        return new BillingOrderStatus(
            order.Id,
            order.UserId,
            order.SubjectId,
            order.Provider,
            order.ExternalOrderId,
            order.ExternalTransactionNo,
            order.Amount,
            order.Currency,
            order.Status,
            order.CreatedUtc,
            order.PaidUtc,
            order.ExternalResponseCode,
            order.ErrorMessage,
            order.BankCode,
            order.CardType);
    }
}
