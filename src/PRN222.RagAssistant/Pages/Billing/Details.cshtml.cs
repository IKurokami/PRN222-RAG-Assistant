using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;

namespace PRN222.RagAssistant.Pages.Billing;

[Authorize]
public sealed class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<DetailsModel> _logger;

    public DetailsModel(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        ILogger<DetailsModel> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _logger = logger;
    }

    public PaymentOrder? Order { get; private set; }

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

        var order = await _dbContext.PaymentOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == user.Id, cancellationToken);

        if (order is null)
        {
            return NotFound();
        }

        Order = order;
        return Page();
    }
}
