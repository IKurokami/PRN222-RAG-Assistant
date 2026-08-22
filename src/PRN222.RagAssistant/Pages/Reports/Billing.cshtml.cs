using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Application.Models;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Pages.Reports;

[Authorize(Roles = AppRoles.Admin)]
public sealed class BillingModel : PageModel
{
    private readonly IBillingReportQueryService _billingReportQueryService;

    public BillingModel(IBillingReportQueryService billingReportQueryService)
    {
        _billingReportQueryService = billingReportQueryService;
    }

    public BillingReportSnapshot Report { get; private set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Report = await _billingReportQueryService.GetBillingReportAsync(cancellationToken);
    }
}
