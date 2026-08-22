using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Application.Models;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;

namespace PRN222.RagAssistant.Infrastructure.Billing;

public sealed class VnPayBillingService : IBillingService
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);
    private readonly ApplicationDbContext _dbContext;
    private readonly BillingOptions _billingOptions;
    private readonly VnPayBillingOptions _options;
    private readonly ILogger<VnPayBillingService> _logger;
    private readonly IUserQuotaService _userQuotaService;

    public VnPayBillingService(
        ApplicationDbContext dbContext,
        IOptions<BillingOptions> billingOptions,
        IOptions<VnPayBillingOptions> options,
        ILogger<VnPayBillingService> logger,
        IUserQuotaService userQuotaService)
    {
        _dbContext = dbContext;
        _billingOptions = billingOptions.Value;
        _options = options.Value;
        _logger = logger;
        _userQuotaService = userQuotaService;
    }

    public async Task<BillingOrderResult> CreateOrderAsync(
        CreateBillingOrderRequest request,
        CancellationToken cancellationToken)
    {
        EnsureEnabled();

        if (request.Amount <= 0 || request.QuotaUnits <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Amount and quota units must be positive.");
        }

        if (!string.Equals(request.Currency, "VND", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("VNPay currently supports VND for this integration.", nameof(request));
        }

        if (!await _dbContext.Users.AsNoTracking().AnyAsync(u => u.Id == request.UserId, cancellationToken))
        {
            throw new InvalidOperationException("Cannot create a billing order for a user that does not exist.");
        }

        var externalOrderId = GenerateExternalOrderId();
        var order = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            SubjectId = request.SubjectId,
            Provider = "VNPay",
            ExternalOrderId = externalOrderId,
            Amount = request.Amount,
            Currency = request.Currency.ToUpperInvariant(),
            Status = "Pending",
            CreatedUtc = DateTime.UtcNow,
            MetadataJson = JsonSerializer.Serialize(new { quotaUnits = request.QuotaUnits })
        };

        _dbContext.PaymentOrders.Add(order);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var checkoutUrl = BuildCheckoutUrl(order, request);
        _logger.LogInformation(
            "Created VNPay order {OrderId}, ExternalOrderId={ExternalOrderId}, Amount={Amount} {Currency}, QuotaUnits={QuotaUnits}",
            order.Id,
            externalOrderId,
            request.Amount,
            request.Currency,
            request.QuotaUnits);

        return new BillingOrderResult(order.Id, externalOrderId, checkoutUrl);
    }

    public async Task<BillingOrderStatus?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken)
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
        EnsureEnabled();

        if (!VnPayHashHelper.VerifySecureHash(request.CallbackParameters, _options.HashSecret))
        {
            throw new InvalidOperationException("Invalid VNPay return signature.");
        }

        var order = await FindCallbackOrderAsync(request, cancellationToken)
            ?? throw new InvalidOperationException("Payment order was not found.");

        ValidateMerchantCode(request.CallbackParameters);
        ValidateAmount(order, request.CallbackParameters);

        // VNPay Return URL is presentation-only. The authoritative state transition is IPN.
        var current = Map(order);
        if (string.Equals(order.Status, "Paid", StringComparison.Ordinal))
        {
            return current;
        }

        var responseCode = GetCallbackValue(request.CallbackParameters, "vnp_ResponseCode");
        var transactionStatus = GetCallbackValue(request.CallbackParameters, "vnp_TransactionStatus");
        var displayStatus = IsSuccessfulPayment(responseCode, transactionStatus) ? "Pending" : "Failed";

        return current with
        {
            Status = displayStatus,
            ExternalResponseCode = responseCode
        };
    }

    public async Task<BillingWebhookResult> ProcessWebhookAsync(
        ProcessWebhookRequest request,
        CancellationToken cancellationToken)
    {
        if (!_billingOptions.Enabled)
        {
            return Reject("99", "Billing is disabled");
        }

        if (!string.Equals(request.Provider, "VNPay", StringComparison.OrdinalIgnoreCase))
        {
            return Reject("99", "Unsupported provider");
        }

        if (!VnPayHashHelper.VerifySecureHash(request.CallbackParameters, _options.HashSecret))
        {
            _logger.LogWarning("Rejected VNPay IPN because the signature is invalid");
            return Reject("97", "Invalid signature");
        }

        try
        {
            ValidateMerchantCode(request.CallbackParameters);

            var externalOrderId = GetCallbackValue(request.CallbackParameters, "vnp_TxnRef");
            if (string.IsNullOrWhiteSpace(externalOrderId))
            {
                return Reject("99", "Missing order reference");
            }

            var order = await _dbContext.PaymentOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.ExternalOrderId == externalOrderId, cancellationToken);

            if (order is null)
            {
                return Reject("01", "Order not found");
            }

            if (!string.Equals(order.Status, "Pending", StringComparison.Ordinal))
            {
                return new BillingWebhookResult(true, "02", "Order already confirmed");
            }

            try
            {
                ValidateAmount(order, request.CallbackParameters);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Rejected VNPay IPN because amount does not match for Order {OrderId}", order.Id);
                return Reject("04", "Invalid amount");
            }

            var responseCode = GetCallbackValue(request.CallbackParameters, "vnp_ResponseCode");
            var transactionStatus = GetCallbackValue(request.CallbackParameters, "vnp_TransactionStatus");

            if (IsSuccessfulPayment(responseCode, transactionStatus))
            {
                var finalized = await TryFinalizePaidOrderAsync(order, request.CallbackParameters, cancellationToken);
                return finalized
                    ? new BillingWebhookResult(true, "00", "Confirm Success")
                    : new BillingWebhookResult(true, "02", "Order already confirmed");
            }

            var failed = await TryFinalizeFailedOrderAsync(order, request.CallbackParameters, cancellationToken);
            return failed
                ? new BillingWebhookResult(true, "00", "Confirm Success")
                : new BillingWebhookResult(true, "02", "Order already confirmed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while processing VNPay IPN");
            return Reject("99", "Internal error");
        }
    }

    private async Task<PaymentOrder?> FindCallbackOrderAsync(
        ProcessReturnRequest request,
        CancellationToken cancellationToken)
    {
        var externalOrderId = GetCallbackValue(request.CallbackParameters, "vnp_TxnRef");
        if (string.IsNullOrWhiteSpace(externalOrderId))
        {
            return null;
        }

        var order = await _dbContext.PaymentOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.ExternalOrderId == externalOrderId, cancellationToken);

        if (order is not null && request.OrderId != Guid.Empty && order.Id != request.OrderId)
        {
            throw new InvalidOperationException("VNPay return order reference does not match the requested order.");
        }

        return order;
    }

    private async Task<bool> TryFinalizePaidOrderAsync(
        PaymentOrder order,
        IReadOnlyDictionary<string, string?> callbackParameters,
        CancellationToken cancellationToken)
    {
        var quotaUnits = GetQuotaUnits(order);
        var paidUtc = DateTime.UtcNow;
        var responseCode = GetCallbackValue(callbackParameters, "vnp_ResponseCode");
        var transactionNo = GetCallbackValue(callbackParameters, "vnp_TransactionNo");
        var bankCode = GetCallbackValue(callbackParameters, "vnp_BankCode");
        var cardType = GetCallbackValue(callbackParameters, "vnp_CardType");
        var metadataJson = BuildPaidMetadata(quotaUnits, callbackParameters);

        IDbContextTransaction? transaction = null;
        if (_dbContext.Database.IsRelational())
        {
            transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        }

        try
        {
            bool claimed;
            if (_dbContext.Database.IsRelational())
            {
                var affected = await _dbContext.PaymentOrders
                    .Where(o => o.Id == order.Id && o.Status == "Pending")
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(o => o.Status, "Paid")
                            .SetProperty(o => o.ExternalResponseCode, responseCode)
                            .SetProperty(o => o.ExternalTransactionNo, transactionNo)
                            .SetProperty(o => o.BankCode, bankCode)
                            .SetProperty(o => o.CardType, cardType)
                            .SetProperty(o => o.PaidUtc, (DateTime?)paidUtc)
                            .SetProperty(o => o.ErrorMessage, (string?)null)
                            .SetProperty(o => o.MetadataJson, metadataJson),
                        cancellationToken);
                claimed = affected == 1;
            }
            else
            {
                var tracked = await _dbContext.PaymentOrders.FirstAsync(o => o.Id == order.Id, cancellationToken);
                claimed = string.Equals(tracked.Status, "Pending", StringComparison.Ordinal);
                if (claimed)
                {
                    ApplyPaidState(tracked, responseCode, transactionNo, bankCode, cardType, paidUtc, metadataJson);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
            }

            if (!claimed)
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }
                return false;
            }

            await _userQuotaService.GrantQuotaAsync(order.UserId, quotaUnits, cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            ApplyPaidState(order, responseCode, transactionNo, bankCode, cardType, paidUtc, metadataJson);
            _logger.LogInformation(
                "Finalized VNPay order {OrderId} exactly once and granted {QuotaUnits} quota to User {UserId}",
                order.Id,
                quotaUnits,
                order.UserId);
            return true;
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private async Task<bool> TryFinalizeFailedOrderAsync(
        PaymentOrder order,
        IReadOnlyDictionary<string, string?> callbackParameters,
        CancellationToken cancellationToken)
    {
        var responseCode = GetCallbackValue(callbackParameters, "vnp_ResponseCode");
        var errorMessage = $"VNPay response code: {responseCode}";

        if (_dbContext.Database.IsRelational())
        {
            var affected = await _dbContext.PaymentOrders
                .Where(o => o.Id == order.Id && o.Status == "Pending")
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(o => o.Status, "Failed")
                        .SetProperty(o => o.ExternalResponseCode, responseCode)
                        .SetProperty(o => o.ErrorMessage, errorMessage)
                        .SetProperty(o => o.PaidUtc, (DateTime?)null),
                    cancellationToken);
            return affected == 1;
        }

        var tracked = await _dbContext.PaymentOrders.FirstAsync(o => o.Id == order.Id, cancellationToken);
        if (!string.Equals(tracked.Status, "Pending", StringComparison.Ordinal))
        {
            return false;
        }

        tracked.Status = "Failed";
        tracked.ExternalResponseCode = responseCode;
        tracked.ErrorMessage = errorMessage;
        tracked.PaidUtc = null;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private void ValidateMerchantCode(IReadOnlyDictionary<string, string?> callbackParameters)
    {
        var tmnCode = GetCallbackValue(callbackParameters, "vnp_TmnCode");
        if (!string.Equals(tmnCode, _options.TmnCode, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("VNPay terminal code does not match this merchant.");
        }
    }

    private static void ValidateAmount(
        PaymentOrder order,
        IReadOnlyDictionary<string, string?> callbackParameters)
    {
        var amountText = GetCallbackValue(callbackParameters, "vnp_Amount");
        if (!long.TryParse(amountText, NumberStyles.None, CultureInfo.InvariantCulture, out var callbackAmount))
        {
            throw new InvalidOperationException("VNPay amount is invalid.");
        }

        var expectedAmount = checked(order.Amount * 100);
        if (callbackAmount != expectedAmount)
        {
            throw new InvalidOperationException("VNPay amount does not match the order amount.");
        }
    }

    private static bool IsSuccessfulPayment(string responseCode, string transactionStatus) =>
        string.Equals(responseCode, "00", StringComparison.Ordinal)
        && string.Equals(transactionStatus, "00", StringComparison.Ordinal);

    private static int GetQuotaUnits(PaymentOrder order)
    {
        try
        {
            using var document = JsonDocument.Parse(order.MetadataJson);
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

        throw new InvalidOperationException($"Order '{order.Id}' does not contain valid immutable quota metadata.");
    }

    private static string BuildPaidMetadata(
        int quotaUnits,
        IReadOnlyDictionary<string, string?> callbackParameters) =>
        JsonSerializer.Serialize(new
        {
            quotaUnits,
            payment = new
            {
                amount = GetCallbackValue(callbackParameters, "vnp_Amount"),
                transactionNo = GetCallbackValue(callbackParameters, "vnp_TransactionNo"),
                bankCode = GetCallbackValue(callbackParameters, "vnp_BankCode"),
                cardType = GetCallbackValue(callbackParameters, "vnp_CardType"),
                responseCode = GetCallbackValue(callbackParameters, "vnp_ResponseCode"),
                transactionStatus = GetCallbackValue(callbackParameters, "vnp_TransactionStatus"),
                payDate = GetCallbackValue(callbackParameters, "vnp_PayDate")
            }
        });

    private Uri BuildCheckoutUrl(PaymentOrder order, CreateBillingOrderRequest request)
    {
        var sanitizedOrderInfo = SanitizeOrderInfo(request.Description);
        if (string.IsNullOrWhiteSpace(sanitizedOrderInfo))
        {
            sanitizedOrderInfo = $"Nap quota PRN222 don {order.ExternalOrderId}";
        }

        if (request.ReturnUrl.AbsoluteUri.Length > 255)
        {
            throw new InvalidOperationException("VNPay return URL exceeds the 255 character limit.");
        }

        var vietnamNow = DateTimeOffset.UtcNow.ToOffset(VietnamOffset);
        var parameters = new Dictionary<string, string?>
        {
            ["vnp_Version"] = _options.Version,
            ["vnp_Command"] = _options.Command,
            ["vnp_TmnCode"] = _options.TmnCode,
            ["vnp_Amount"] = checked(order.Amount * 100).ToString(CultureInfo.InvariantCulture),
            ["vnp_CurrCode"] = order.Currency,
            ["vnp_TxnRef"] = order.ExternalOrderId,
            ["vnp_OrderInfo"] = sanitizedOrderInfo,
            ["vnp_IpAddr"] = request.IpAddress,
            ["vnp_ReturnUrl"] = request.ReturnUrl.AbsoluteUri,
            ["vnp_CreateDate"] = vietnamNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
            ["vnp_ExpireDate"] = vietnamNow.AddMinutes(15).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
            ["vnp_Locale"] = _options.Locale,
            ["vnp_OrderType"] = _options.OrderType
        };

        var url = VnPayHashHelper.BuildFullUrl(_options.BaseUrl, parameters, _options.HashSecret);
        return new Uri(url, UriKind.Absolute);
    }

    private static string SanitizeOrderInfo(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = text
            .Replace('Đ', 'D')
            .Replace('đ', 'd')
            .Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        var ascii = new StringBuilder(builder.Length);
        foreach (var c in builder.ToString().Normalize(NormalizationForm.FormC))
        {
            if (c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or ' ' or '-' or '_')
            {
                ascii.Append(c);
            }
        }

        var result = ascii.ToString().Trim();
        return result.Length <= 255 ? result : result[..255].TrimEnd();
    }

    private static string GetCallbackValue(
        IReadOnlyDictionary<string, string?> parameters,
        string key) =>
        parameters.TryGetValue(key, out var value) ? value?.Trim() ?? string.Empty : string.Empty;

    private static void ApplyPaidState(
        PaymentOrder order,
        string responseCode,
        string transactionNo,
        string bankCode,
        string cardType,
        DateTime paidUtc,
        string metadataJson)
    {
        order.Status = "Paid";
        order.ExternalResponseCode = responseCode;
        order.ExternalTransactionNo = transactionNo;
        order.BankCode = bankCode;
        order.CardType = cardType;
        order.PaidUtc = paidUtc;
        order.ErrorMessage = null;
        order.MetadataJson = metadataJson;
    }

    private void EnsureEnabled()
    {
        if (!_billingOptions.Enabled)
        {
            throw new InvalidOperationException("Billing is disabled for this environment.");
        }
    }

    private static BillingWebhookResult Reject(string responseCode, string message) =>
        new(false, responseCode, message);

    private static string GenerateExternalOrderId()
    {
        var date = DateTimeOffset.UtcNow.ToOffset(VietnamOffset).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        return $"PRN222-{date}-{Guid.NewGuid():N}"[..27];
    }

    private static BillingOrderStatus Map(PaymentOrder order) =>
        new(
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
