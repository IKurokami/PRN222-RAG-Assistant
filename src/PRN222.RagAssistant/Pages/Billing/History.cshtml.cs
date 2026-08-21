using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;

namespace PRN222.RagAssistant.Pages.Billing;

[Authorize]
public sealed class HistoryModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<HistoryModel> _logger;

    public HistoryModel(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        ILogger<HistoryModel> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _logger = logger;
    }

    public List<OrderViewModel> Orders { get; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var orders = await _dbContext.PaymentOrders
            .AsNoTracking()
            .Where(o => o.UserId == user.Id)
            .OrderByDescending(o => o.CreatedUtc)
            .ToListAsync(cancellationToken);

        Orders.Clear();
        foreach (var order in orders)
        {
            Orders.Add(new OrderViewModel(order));
        }

        return Page();
    }

    public sealed class OrderViewModel
    {
        public OrderViewModel(PaymentOrder order)
        {
            Id = order.Id;
            CreatedUtc = order.CreatedUtc;
            SubjectId = order.SubjectId;
            Amount = order.Amount;
            Currency = order.Currency;
            Status = order.Status;
            ExternalOrderId = order.ExternalOrderId;
            ExternalTransactionNo = order.ExternalTransactionNo;
            PaidUtc = order.PaidUtc;
            AmountDisplay = order.Amount.ToString("N0", System.Globalization.CultureInfo.CreateSpecificCulture("vi-VN")) + " " + order.Currency;
        }

        public Guid Id { get; }
        public Guid? SubjectId { get; }
        public DateTime CreatedUtc { get; }
        public long Amount { get; }
        public string Currency { get; }
        public string Status { get; }
        public string ExternalOrderId { get; }
        public string? ExternalTransactionNo { get; }
        public DateTime? PaidUtc { get; }
        public string AmountDisplay { get; }
    }
}
