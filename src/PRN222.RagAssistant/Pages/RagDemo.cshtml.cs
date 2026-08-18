using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Application.Models;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace PRN222.RagAssistant.Pages;

public class RagDemoModel : PageModel
{
    private readonly IRagQueryService _ragService;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<RagDemoModel> _logger;

    public RagDemoModel(
        IRagQueryService ragService,
        ApplicationDbContext dbContext,
        ILogger<RagDemoModel> logger)
    {
        _ragService = ragService;
        _dbContext = dbContext;
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
        var user = await GetOrCreateDemoUserAsync();
        var session = await GetOrCreateDemoSessionAsync(user.Id);

            var result = await _ragService.AskAsync(
                user.Id,
                session.Id,
                Question);

            Answer = result.Answer;
            Citations = result.Citations;
            Error = null;

            _logger.LogInformation(
                "RAG demo answered. Question={Question}, Chunks={Chunks}, Citations={Citations}",
                Question.Length > 50 ? Question[..50] + "..." : Question,
                Citations.Count,
                Citations.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RAG demo failed");
            Error = $"Lỗi: {ex.Message}";
            Answer = null;
            Citations = null;
        }

        IsProcessing = false;
        return Page();
    }

    private async Task<ApplicationUser> GetOrCreateDemoUserAsync()
    {
        var email = "demo@prn222.local";
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = Guid.CreateVersion7(),
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
        }

        return user;
    }

    private async Task<ChatSession> GetOrCreateDemoSessionAsync(Guid userId)
    {
        var session = await _dbContext.ChatSessions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Title == "Demo Session");

        if (session is null)
        {
            session = new ChatSession
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                Title = "Demo Session",
                CreatedAtUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                UpdatedAtUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
            };
            _dbContext.ChatSessions.Add(session);
            await _dbContext.SaveChangesAsync();
        }

        return session;
    }

}
