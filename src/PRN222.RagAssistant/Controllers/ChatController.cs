using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Application.Models;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Domain.Enums;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Controllers;

[Authorize]
public sealed class ChatController : Controller
{
    private readonly IRagQueryService _ragQueryService;
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        IRagQueryService ragQueryService,
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        ILogger<ChatController> logger)
    {
        _ragQueryService = ragQueryService;
        _dbContext = dbContext;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<IActionResult> Index(Guid? subjectId, Guid? sessionId, CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        var roles = await _userManager.GetRolesAsync(user);
        var isAdmin = roles.Contains(AppRoles.Admin);

        // Admin may access any subject; others only active subjects.
        var subjectQuery = _dbContext.Subjects.AsNoTracking();
        if (!isAdmin)
        {
            subjectQuery = subjectQuery.Where(s => s.IsActive);
        }
        var subjects = await subjectQuery
            .OrderBy(s => s.Code)
            .ToListAsync(cancellationToken);

        if (subjects.Count == 0)
        {
            ViewBag.ErrorMessage = "Chưa có Môn học nào được kích hoạt trong hệ thống.";
            return View(new ChatViewModel());
        }

        var selectedSubject = subjects.FirstOrDefault(s => s.Id == subjectId) ?? subjects.First();

        // Non-admin users must not access an inactive subject.
        if (!isAdmin && !selectedSubject.IsActive)
        {
            return Forbid();
        }

        var userSessions = await _dbContext.ChatSessions
            .AsNoTracking()
            .Where(s => s.UserId == user.Id && (s.SubjectId == null || s.SubjectId == selectedSubject.Id))
            .OrderByDescending(s => s.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        ChatSession? activeSession = null;
        if (sessionId.HasValue)
        {
            activeSession = userSessions.FirstOrDefault(s => s.Id == sessionId.Value);
        }

        if (activeSession == null)
        {
            var createdSessionId = await _ragQueryService.GetOrCreateUserSessionAsync(user.Id, selectedSubject.Id, cancellationToken);
            activeSession = await _dbContext.ChatSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == createdSessionId, cancellationToken);

            if (userSessions.All(s => s.Id != createdSessionId) && activeSession != null)
            {
                userSessions.Insert(0, activeSession);
            }
        }

        var messages = new List<ChatMessageItemViewModel>();
        if (activeSession != null)
        {
            var rawMessages = await _dbContext.ChatMessages
                .AsNoTracking()
                .Where(m => m.ChatSessionId == activeSession.Id)
                .OrderBy(m => m.CreatedAtUtc)
                .ToListAsync(cancellationToken);

            var messageIds = rawMessages.Select(m => m.Id).ToList();

            var citations = await _dbContext.MessageCitations
                .AsNoTracking()
                .Where(c => messageIds.Contains(c.ChatMessageId))
                .Join(_dbContext.DocumentChunks.AsNoTracking(),
                    mc => mc.DocumentChunkId,
                    chunk => chunk.Id,
                    (mc, chunk) => new { mc, chunk })
                .Join(_dbContext.Documents.AsNoTracking(),
                    combined => combined.chunk.DocumentId,
                    doc => doc.Id,
                    (combined, doc) => new CitationViewModel
                    {
                        ChatMessageId = combined.mc.ChatMessageId,
                        Rank = combined.mc.Rank,
                        DocumentTitle = doc.Title,
                        PageNumber = combined.chunk.PageNumber,
                        SlideNumber = combined.chunk.SlideNumber,
                        ChunkContent = combined.chunk.Content
                    })
                .OrderBy(c => c.Rank)
                .ToListAsync(cancellationToken);

            var citationsLookup = citations.ToLookup(c => c.ChatMessageId);

            foreach (var m in rawMessages)
            {
                messages.Add(new ChatMessageItemViewModel
                {
                    Id = m.Id,
                    Role = m.Role,
                    Content = m.Content,
                    CreatedAtUtc = m.CreatedAtUtc,
                    Citations = citationsLookup[m.Id].ToList()
                });
            }
        }

        var viewModel = new ChatViewModel
        {
            Subjects = subjects,
            SelectedSubjectId = selectedSubject.Id,
            SelectedSubjectName = selectedSubject.Name,
            SelectedSubjectCode = selectedSubject.Code,
            Sessions = userSessions,
            ActiveSession = activeSession,
            Messages = messages
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ask([FromBody] AskRequestDto dto, CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized(new { message = "Vui lòng đăng nhập." });
        }

        if (string.IsNullOrWhiteSpace(dto.Question))
        {
            return BadRequest(new { message = "Câu hỏi không được để trống." });
        }

        // Verify subject access: non-admin users may only chat on active subjects.
        var roles = await _userManager.GetRolesAsync(user);
        if (!roles.Contains(AppRoles.Admin))
        {
            var subjectExists = await _dbContext.Subjects
                .AsNoTracking()
                .AnyAsync(s => s.Id == dto.SubjectId && s.IsActive, cancellationToken);

            if (!subjectExists)
            {
                return Forbid();
            }
        }

        try
        {
            var answer = await _ragQueryService.AskAsync(
                user.Id,
                dto.SessionId,
                dto.Question,
                dto.SubjectId,
                cancellationToken);

            return Json(new
            {
                success = true,
                answer = answer.Answer,
                citations = answer.Citations.Select(c => new
                {
                    documentTitle = c.DocumentTitle,
                    pageNumber = c.PageNumber,
                    slideNumber = c.SlideNumber,
                    rank = c.Rank,
                    excerpt = c.Excerpt
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat request failed for User {UserId}", user.Id);
            return StatusCode(500, new { message = "Đã xảy ra lỗi khi xử lý câu hỏi: " + ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NewSession(Guid subjectId, CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        var nowUtc = DateTime.UtcNow;
        var session = new ChatSession
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            SubjectId = subjectId,
            Title = string.Empty,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };

        _dbContext.ChatSessions.Add(session);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index), new { subjectId, sessionId = session.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSession(Guid sessionId, Guid subjectId, CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        var session = await _dbContext.ChatSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == user.Id, cancellationToken);

        if (session != null)
        {
            _dbContext.ChatSessions.Remove(session);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return RedirectToAction(nameof(Index), new { subjectId });
    }
}

public sealed class ChatViewModel
{
    public List<Subject> Subjects { get; set; } = new();
    public Guid SelectedSubjectId { get; set; }
    public string SelectedSubjectName { get; set; } = string.Empty;
    public string SelectedSubjectCode { get; set; } = string.Empty;
    public List<ChatSession> Sessions { get; set; } = new();
    public ChatSession? ActiveSession { get; set; }
    public List<ChatMessageItemViewModel> Messages { get; set; } = new();
}

public sealed class ChatMessageItemViewModel
{
    public Guid Id { get; set; }
    public ChatMessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public List<CitationViewModel> Citations { get; set; } = new();
}

public sealed class CitationViewModel
{
    public Guid ChatMessageId { get; set; }
    public int Rank { get; set; }
    public string DocumentTitle { get; set; } = string.Empty;
    public int? PageNumber { get; set; }
    public int? SlideNumber { get; set; }
    public string ChunkContent { get; set; } = string.Empty;
}

public sealed class AskRequestDto
{
    public Guid SessionId { get; set; }
    public Guid SubjectId { get; set; }
    public string Question { get; set; } = string.Empty;
}
