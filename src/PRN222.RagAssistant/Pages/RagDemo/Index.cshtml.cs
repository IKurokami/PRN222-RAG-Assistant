using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Application.Models;
using PRN222.RagAssistant.Domain.Entities;

namespace PRN222.RagAssistant.Pages.RagDemo;

[Authorize]
public sealed class IndexModel : PageModel
{
    private readonly IRagQueryService _ragService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        IRagQueryService ragService,
        UserManager<ApplicationUser> userManager,
        ILogger<IndexModel> logger)
    {
        _ragService = ragService;
        _userManager = userManager;
        _logger = logger;
    }

    [BindProperty]
    public string Question { get; set; } = string.Empty;

    public bool IsProcessing { get; set; }
    public string? Answer { get; set; }
    public IReadOnlyList<RagCitation>? Citations { get; set; }
    public string? Error { get; set; }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Question))
        {
            Error = "Vui lòng nhập câu hỏi.";
            return Page();
        }

        IsProcessing = true;

        try
        {
            var user = await _userManager.GetUserAsync(User)
                ?? throw new UnauthorizedAccessException("User not found");
            var sessionId = await _ragService.GetOrCreateUserSessionAsync(user.Id);

            var result = await _ragService.AskAsync(
                user.Id,
                sessionId,
                Question);

            Answer = result.Answer;
            Citations = result.Citations;
            Error = null;

            _logger.LogInformation(
                "RAG demo answered. User={UserId}, Question={Question}, Chunks={Chunks}",
                user.Id,
                Question.Length > 50 ? Question[..50] + "..." : Question,
                Citations.Count);
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("RAG demo unauthorized access attempt");
            Error = "Vui lòng đăng nhập để sử dụng tính năng này.";
            Answer = null;
            Citations = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RAG demo failed");
            Error = "Đã xảy ra lỗi khi xử lý câu hỏi. Vui lòng thử lại sau.";
            Answer = null;
            Citations = null;
        }

        IsProcessing = false;
        return Page();
    }
}
