using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Application.Models;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Infrastructure.Billing;

namespace PRN222.RagAssistant.Pages.Billing;

[Authorize]
public sealed class CreateModel : PageModel
{
    private readonly IBillingService _billingService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserQuotaService _userQuotaService;
    private readonly BillingOptions _billingOptions;
    private readonly ILogger<CreateModel> _logger;

    public CreateModel(
        IBillingService billingService,
        UserManager<ApplicationUser> userManager,
        IUserQuotaService userQuotaService,
        IOptions<BillingOptions> billingOptions,
        ILogger<CreateModel> logger)
    {
        _billingService = billingService;
        _userManager = userManager;
        _userQuotaService = userQuotaService;
        _billingOptions = billingOptions.Value;
        _logger = logger;
    }

    public List<BillingPlanViewModel> Plans { get; } =
    [
        new(1, "Gói 50 câu hỏi RAG", 50_000, "VND", 50, "50.000 VND"),
        new(2, "Gói 100 câu hỏi RAG", 100_000, "VND", 100, "100.000 VND"),
        new(3, "Gói 200 câu hỏi RAG", 200_000, "VND", 200, "200.000 VND")
    ];

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? Message { get; private set; }
    public int CurrentQuota { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (!_billingOptions.Enabled)
        {
            Message = "Chức năng thanh toán đang tắt trong môi trường này.";
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is not null)
        {
            CurrentQuota = await _userQuotaService.GetRemainingQuotaAsync(user.Id, cancellationToken);
        }
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!_billingOptions.Enabled)
        {
            Message = "Chức năng thanh toán đang tắt trong môi trường này.";
            return Page();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        CurrentQuota = await _userQuotaService.GetRemainingQuotaAsync(user.Id, cancellationToken);

        var plan = Plans.FirstOrDefault(p => p.Id == Input.PlanId);
        if (plan is null)
        {
            ModelState.AddModelError("Input.PlanId", "Vui lòng chọn gói thanh toán.");
            return Page();
        }

        var baseUrl = GetBaseUrl();
        var returnPath = Url.Page("/Billing/Return", pageHandler: null) ?? "/Billing/Return";
        var returnUrl = new Uri(baseUrl, returnPath);
        var remoteIp = HttpContext.Connection.RemoteIpAddress;
        if (remoteIp?.IsIPv4MappedToIPv6 == true)
        {
            remoteIp = remoteIp.MapToIPv4();
        }
        var ipAddress = remoteIp?.ToString() ?? "127.0.0.1";

        var request = new CreateBillingOrderRequest(
            user.Id,
            null,
            plan.Amount,
            plan.Currency,
            string.IsNullOrWhiteSpace(Input.Description) ? $"Mua {plan.Name}" : Input.Description.Trim(),
            returnUrl,
            ipAddress,
            plan.Queries);

        try
        {
            var result = await _billingService.CreateOrderAsync(request, cancellationToken);
            _logger.LogInformation(
                "Created VNPay order {OrderId} for User {UserId}, Amount {Amount} {Currency}, QuotaUnits={QuotaUnits}",
                result.OrderId,
                user.Id,
                plan.Amount,
                plan.Currency,
                plan.Queries);

            return Redirect(result.CheckoutUrl.AbsoluteUri);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create VNPay order for User {UserId}", user.Id);
            Message = "Không thể tạo yêu cầu thanh toán. Vui lòng thử lại sau.";
            return Page();
        }
    }

    private Uri GetBaseUrl()
    {
        if (!string.IsNullOrWhiteSpace(_billingOptions.BaseUrl)
            && Uri.TryCreate(_billingOptions.BaseUrl, UriKind.Absolute, out var configuredUri))
        {
            return configuredUri;
        }

        var request = HttpContext.Request;
        return new Uri($"{request.Scheme}://{request.Host}", UriKind.Absolute);
    }

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Vui lòng chọn gói thanh toán.")]
        public int PlanId { get; set; }

        [StringLength(255, ErrorMessage = "Ghi chú thanh toán tối đa 255 ký tự.")]
        public string Description { get; set; } = string.Empty;
    }
}

public sealed class BillingPlanViewModel(
    int id,
    string name,
    long amount,
    string currency,
    int queries,
    string amountDisplay)
{
    public int Id { get; } = id;
    public string Name { get; } = name;
    public long Amount { get; } = amount;
    public string Currency { get; } = currency;
    public int Queries { get; } = queries;
    public string AmountDisplay { get; } = amountDisplay;
}
