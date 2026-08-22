using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Application.Models;

namespace PRN222.RagAssistant.Pages.Billing;

[IgnoreAntiforgeryToken]
[AllowAnonymous]
public sealed class WebhookModel : PageModel
{
    private readonly IBillingService _billingService;
    private readonly ILogger<WebhookModel> _logger;

    public WebhookModel(IBillingService billingService, ILogger<WebhookModel> logger)
    {
        _billingService = billingService;
        _logger = logger;
    }

    public Task<IActionResult> OnGetAsync(CancellationToken cancellationToken) =>
        ProcessIpnAsync(cancellationToken);

    public Task<IActionResult> OnPostAsync(CancellationToken cancellationToken) =>
        ProcessIpnAsync(cancellationToken);

    private async Task<IActionResult> ProcessIpnAsync(CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var kvp in Request.Query)
        {
            parameters[kvp.Key] = kvp.Value;
        }

        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync(cancellationToken);
            foreach (var kvp in form)
            {
                parameters[kvp.Key] = kvp.Value;
            }
        }

        var result = await _billingService.ProcessWebhookAsync(
            new ProcessWebhookRequest("VNPay", parameters),
            cancellationToken);

        if (!result.Success)
        {
            _logger.LogWarning(
                "VNPay IPN rejected with RspCode={ResponseCode}: {Message}",
                result.ResponseCode,
                result.Message);
        }

        return new JsonResult(new { RspCode = result.ResponseCode, Message = result.Message });
    }
}
