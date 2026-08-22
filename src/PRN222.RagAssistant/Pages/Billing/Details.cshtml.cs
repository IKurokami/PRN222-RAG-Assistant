using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Application.Models;
using PRN222.RagAssistant.Domain.Entities;

namespace PRN222.RagAssistant.Pages.Billing;

[Authorize]
public sealed class DetailsModel : PageModel
{
    private readonly IBillingService _billingService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<DetailsModel> _logger;

    public DetailsModel(
        IBillingService billingService,
        UserManager<ApplicationUser> userManager,
        ILogger<DetailsModel> logger)
    {
        _billingService = billingService;
        _userManager = userManager;
        _logger = logger;
    }

    public BillingOrderStatus? Order { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid orderId, CancellationToken cancellationToken)
    {
        if (orderId == Guid.Empty)
        {
            return NotFound();
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var order = await _billingService.GetOrderAsync(orderId, cancellationToken);
        if (order is null || order.UserId != user.Id)
        {
            return NotFound();
        }

        Order = order;
        return Page();
    }
}
