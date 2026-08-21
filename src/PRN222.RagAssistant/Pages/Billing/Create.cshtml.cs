using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Application.Models;
using PRN222.RagAssistant.Domain.Entities;

namespace PRN222.RagAssistant.Pages.Billing;

[Authorize]
public sealed class CreateModel : PageModel
{
    private readonly IBillingService _billingService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CreateModel> _logger;

    public CreateModel(
        IBillingService billingService,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger<CreateModel> logger)
    {
        _billingService = billingService;
        _userManager = userManager;
        _configuration = configuration;
        _logger = logger;
    }

    public List<BillingPlanViewModel> Plans { get; } = new()
    {
        new BillingPlanViewModel(1, "Gói 50 câu hỏi RAG", 50_000, "VND", 50, "50.000 VND"),
        new BillingPlanViewModel(2, "Gói 100 câu hỏi RAG", 100_000, "VND", 100, "100.000 VND"),
        new BillingPlanViewModel(3, "Gói 200 câu hỏi RAG", 200_000, "VND", 200, "200.000 VND"),
    };

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? Message { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            Message = "Không tìm thấy thông tin người dùng hiện tại.";
            return Page();
        }

        var plan = Plans.FirstOrDefault(p => p.Id == Input.PlanId);
        if (plan is null)
        {
            ModelState.AddModelError("Input.PlanId", "Vui lòng chọn gói thanh toán.");
            return Page();
        }

        var baseUrl = GetBaseUrl();
        var returnPath = Url.Page("/Billing/Return", pageHandler: null) ?? "/Billing/Return";
        var returnUrl = new Uri(baseUrl, returnPath);
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        var request = new CreateBillingOrderRequest(
            user.Id,
            Input.SubjectId,
            plan.Amount,
            plan.Currency,
            string.IsNullOrWhiteSpace(Input.Description) ? $"Mua {plan.Name}" : Input.Description.Trim(),
            returnUrl,
            ipAddress);

        try
        {
            var result = await _billingService.CreateOrderAsync(request, cancellationToken);
            _logger.LogInformation(
                "Created VNPay order {OrderId} for User {UserId}, Amount {Amount} {Currency}, ReturnUrl={ReturnUrl}",
                result.OrderId, user.Id, plan.Amount, plan.Currency, returnUrl);

            return Redirect(result.CheckoutUrl.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create VNPay order for User {UserId}", user.Id);
            Message = "Không thể tạo yêu cầu thanh toán. Vui lòng thử lại: " + ex.Message;
            return Page();
        }
    }

    private Uri GetBaseUrl()
    {
        var rawUrl = _configuration["Billing:BaseUrl"];
        if (string.IsNullOrWhiteSpace(rawUrl) || !Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri))
        {
            var request = HttpContext.Request;
            var fallback = $"{request.Scheme}://{request.Host}";
            return new Uri(fallback, UriKind.Absolute);
        }
        return uri;
    }

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Vui lòng chọn gói thanh toán.")]
        public int PlanId { get; set; }

        public string Description { get; set; } = string.Empty;

        public Guid? SubjectId { get; set; }
    }
}

public sealed class BillingPlanViewModel
{
    public BillingPlanViewModel(int id, string name, long amount, string currency, int queries, string amountDisplay)
    {
        Id = id;
        Name = name;
        Amount = amount;
        Currency = currency;
        Queries = queries;
        AmountDisplay = amountDisplay;
    }

    public int Id { get; }
    public string Name { get; }
    public long Amount { get; }
    public string Currency { get; }
    public int Queries { get; }
    public string AmountDisplay { get; }
}
