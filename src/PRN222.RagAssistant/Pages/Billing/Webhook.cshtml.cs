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

    public WebhookModel(
        IBillingService billingService,
        ILogger<WebhookModel> logger)
    {
        _billingService = billingService;
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        return await ProcessIpnAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        return await ProcessIpnAsync(cancellationToken);
    }

    private async Task<IActionResult> ProcessIpnAsync(CancellationToken cancellationToken)
    {
        // VNPay IPN can send parameters as query string or form data.
        var parameters = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (Request.Query.Count > 0)
        {
            foreach (var kvp in Request.Query)
            {
                parameters[kvp.Key] = kvp.Value;
            }
        }

        if (Request.HasFormContentType && Request.Form.Count > 0)
        {
            foreach (var kvp in Request.Form)
            {
                parameters[kvp.Key] = kvp.Value;
            }
        }

        var provider = "VNPay";
        var result = await _billingService.ProcessWebhookAsync(
            new ProcessWebhookRequest(provider, parameters),
            cancellationToken);

        if (result.Success)
        {
            return new JsonResult(new { RspCode = "00", Message = "Confirm Success" });
        }

        _logger.LogWarning("VNPay webhook rejected: {Message}", result.Message);
        return new JsonResult(new { RspCode = "99", Message = result.Message });
    }
}
