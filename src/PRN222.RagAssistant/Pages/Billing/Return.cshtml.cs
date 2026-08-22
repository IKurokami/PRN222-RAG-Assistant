using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Application.Models;

namespace PRN222.RagAssistant.Pages.Billing;

[AllowAnonymous]
public sealed class ReturnModel : PageModel
{
    private readonly IBillingService _billingService;
    private readonly ILogger<ReturnModel> _logger;

    public ReturnModel(IBillingService billingService, ILogger<ReturnModel> logger)
    {
        _billingService = billingService;
        _logger = logger;
    }

    public BillingOrderStatus? OrderStatus { get; private set; }
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var callbackParameters = Request.Query.ToDictionary(
            kvp => kvp.Key,
            kvp => (string?)kvp.Value,
            StringComparer.Ordinal);

        if (callbackParameters.Count == 0)
        {
            ErrorMessage = "Không tìm thấy thông tin giao dịch trả về.";
            return Page();
        }

        try
        {
            OrderStatus = await _billingService.ProcessReturnAsync(
                new ProcessReturnRequest(Guid.Empty, callbackParameters),
                cancellationToken);

            _logger.LogInformation(
                "VNPay return processed for Order {OrderId}, Status={Status}",
                OrderStatus.OrderId,
                OrderStatus.Status);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "VNPay return verification failed");
            ErrorMessage = "Không thể xác thực kết quả giao dịch. Vui lòng kiểm tra lịch sử giao dịch hoặc thử lại sau.";
        }

        return Page();
    }
}
