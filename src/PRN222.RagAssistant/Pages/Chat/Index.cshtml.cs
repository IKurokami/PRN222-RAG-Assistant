using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Application.Models;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Domain.Enums;

namespace PRN222.RagAssistant.Pages.Chat;

[Authorize]
public sealed class IndexModel : PageModel
{
    private readonly IRagQueryService _ragQueryService;
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        IRagQueryService ragQueryService,
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        ILogger<IndexModel> logger)
    {
        _ragQueryService = ragQueryService;
        _dbContext = dbContext;
        _userManager = userManager;
        _logger = logger;
    }

    public List<Subject> Subjects { get; private set; } = new();
    public Guid SelectedSubjectId { get; private set; }
    public string SelectedSubjectName { get; private set; } = string.Empty;
    public string SelectedSubjectCode { get; private set; } = string.Empty;
    public List<ChatSession> Sessions { get; private set; } = new();
    public ChatSession? ActiveSession { get; private set; }
    public List<ChatMessageItemViewModel> Messages { get; private set; } = new();
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        Guid? subjectId,
        Guid? sessionId,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        Subjects = await _dbContext.Subjects
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Code)
            .ToListAsync(cancellationToken);

        if (Subjects.Count == 0)
        {
            ErrorMessage = "Chưa có Môn học nào được kích hoạt trong hệ thống.";
            return Page();
        }

        var selectedSubject = Subjects.FirstOrDefault(s => s.Id == subjectId) ?? Subjects.First();
        SelectedSubjectId = selectedSubject.Id;
        SelectedSubjectName = selectedSubject.Name;
        SelectedSubjectCode = selectedSubject.Code;

        Sessions = await _dbContext.ChatSessions
            .AsNoTracking()
            .Where(s => s.UserId == user.Id && (s.SubjectId == null || s.SubjectId == selectedSubject.Id))
            .OrderByDescending(s => s.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        if (sessionId.HasValue)
        {
            ActiveSession = Sessions.FirstOrDefault(s => s.Id == sessionId.Value);
        }

        if (ActiveSession == null)
        {
            var createdSessionId = await _ragQueryService.GetOrCreateUserSessionAsync(
                user.Id,
                selectedSubject.Id,
                cancellationToken);

            ActiveSession = await _dbContext.ChatSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == createdSessionId, cancellationToken);

            if (Sessions.All(s => s.Id != createdSessionId) && ActiveSession != null)
            {
                Sessions.Insert(0, ActiveSession);
            }
        }

        if (ActiveSession == null)
        {
            return Page();
        }

        var rawMessages = await _dbContext.ChatMessages
            .AsNoTracking()
            .Where(m => m.ChatSessionId == ActiveSession.Id)
            .OrderBy(m => m.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var messageIds = rawMessages.Select(m => m.Id).ToList();

        var citations = await _dbContext.MessageCitations
            .AsNoTracking()
            .Where(c => messageIds.Contains(c.ChatMessageId))
            .Join(
                _dbContext.DocumentChunks.AsNoTracking(),
                mc => mc.DocumentChunkId,
                chunk => chunk.Id,
                (mc, chunk) => new { mc, chunk })
            .Join(
                _dbContext.Documents.AsNoTracking(),
                combined => combined.chunk.DocumentId,
                doc => doc.Id,
                (combined, doc) => new CitationViewModel
                {
                    ChatMessageId = combined.mc.ChatMessageId,
                    Rank = combined.mc.Rank,
                    DocumentTitle = doc.Title,
                    PageNumber = combined.chunk.PageNumber ?? 1,
                    ChunkContent = combined.chunk.Content
                })
            .OrderBy(c => c.Rank)
            .ToListAsync(cancellationToken);

        var citationsLookup = citations.ToLookup(c => c.ChatMessageId);

        foreach (var message in rawMessages)
        {
            Messages.Add(new ChatMessageItemViewModel
            {
                Id = message.Id,
                Role = message.Role,
                Content = message.Content,
                CreatedAtUtc = message.CreatedAtUtc,
                Citations = citationsLookup[message.Id].ToList()
            });
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAskAsync(
        [FromBody] AskRequestDto dto,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return new JsonResult(new { message = "Vui lòng đăng nhập." })
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
        }

        if (string.IsNullOrWhiteSpace(dto.Question))
        {
            return new JsonResult(new { message = "Câu hỏi không được để trống." })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
        }

        try
        {
            var answer = await _ragQueryService.AskAsync(
                user.Id,
                dto.SessionId,
                dto.Question,
                dto.SubjectId,
                cancellationToken);

            return new JsonResult(new
            {
                success = true,
                answer = answer.Answer,
                citations = answer.Citations.Select(c => new
                {
                    documentId = c.DocumentId,
                    documentChunkId = c.DocumentChunkId,
                    documentTitle = c.DocumentTitle,
                    pageNumber = c.PageNumber ?? 1,
                    slideNumber = c.SlideNumber,
                    rank = c.Rank,
                    excerpt = c.Excerpt
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat request failed for User {UserId}", user.Id);
            return new JsonResult(new { message = "Đã xảy ra lỗi khi xử lý câu hỏi: " + ex.Message })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }
    }

    public async Task<IActionResult> OnPostAskStreamAsync(
        [FromBody] AskRequestDto dto,
        CancellationToken cancellationToken)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        Response.HttpContext.Features
            .Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>()?
            .DisableBuffering();

        async Task SendEventAsync(string eventName, object data)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(data);
            await Response.WriteAsync($"event: {eventName}\ndata: {json}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            await SendEventAsync("error", new { message = "Vui lòng đăng nhập." });
            return new EmptyResult();
        }

        if (string.IsNullOrWhiteSpace(dto.Question))
        {
            await SendEventAsync("error", new { message = "Câu hỏi không được để trống." });
            return new EmptyResult();
        }

        try
        {
            await SendEventAsync("tool_call", new
            {
                id = "call_vectorize",
                tool = "vectorize_query",
                status = "running",
                title = "Mã hóa câu hỏi ngữ nghĩa",
                detail = $"Chuyển đổi câu hỏi \"{dto.Question}\" thành vector embedding"
            });

            await SendEventAsync("tool_call", new
            {
                id = "call_search",
                tool = "search_pgvector",
                status = "running",
                title = "Tìm kiếm tài liệu trong pgvector",
                detail = "So khớp Cosine Distance trên các DocumentChunks thuộc môn học"
            });

            var answer = await _ragQueryService.AskAsync(
                user.Id,
                dto.SessionId,
                dto.Question,
                dto.SubjectId,
                cancellationToken);

            var citations = answer.Citations.Select(c => new
            {
                documentId = c.DocumentId,
                documentChunkId = c.DocumentChunkId,
                documentTitle = c.DocumentTitle,
                pageNumber = c.PageNumber ?? 1,
                slideNumber = c.SlideNumber,
                rank = c.Rank,
                excerpt = c.Excerpt
            }).ToList();

            await SendEventAsync("tool_call", new
            {
                id = "call_retrieval_done",
                tool = "retrieval_result",
                status = "completed",
                title = citations.Count > 0
                    ? $"Đã tìm thấy {citations.Count} đoạn trích tài liệu"
                    : "Không tìm thấy tài liệu phù hợp",
                detail = citations.Count > 0
                    ? string.Join(", ", citations.Select(c => $"{c.documentTitle} (Trang {c.pageNumber})"))
                    : "Không có đoạn trích nào đạt ngưỡng tương đồng tối thiểu (0.3)",
                citations
            });

            await SendEventAsync("tool_call", new
            {
                id = "call_synthesize",
                tool = "llm_completion",
                status = "completed",
                title = "Tổng hợp câu trả lời với AI",
                detail = "Sinh phản hồi có căn cứ (Grounded Context) từ các đoạn trích"
            });

            await SendEventAsync("citations", new { citations });

            var tokens = System.Text.RegularExpressions.Regex.Matches(answer.Answer, @"\S+\s*");
            foreach (System.Text.RegularExpressions.Match match in tokens)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                await SendEventAsync("delta", new { content = match.Value });
                await Task.Delay(18, cancellationToken);
            }

            await SendEventAsync("done", new
            {
                answer = answer.Answer,
                citations
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("Chat stream cancelled for User {UserId}", user.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat stream request failed for User {UserId}", user.Id);
            await SendEventAsync("error", new { message = "Đã xảy ra lỗi: " + ex.Message });
        }

        return new EmptyResult();
    }

    public async Task<IActionResult> OnPostNewSessionAsync(
        Guid subjectId,
        CancellationToken cancellationToken)
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

        return RedirectToPage("/Chat/Index", new { subjectId, sessionId = session.Id });
    }

    public async Task<IActionResult> OnPostDeleteSessionAsync(
        Guid sessionId,
        Guid subjectId,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        var session = await _dbContext.ChatSessions
            .FirstOrDefaultAsync(
                s => s.Id == sessionId && s.UserId == user.Id,
                cancellationToken);

        if (session != null)
        {
            _dbContext.ChatSessions.Remove(session);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return RedirectToPage("/Chat/Index", new { subjectId });
    }
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
    public int PageNumber { get; set; }
    public string ChunkContent { get; set; } = string.Empty;
}

public sealed class AskRequestDto
{
    public Guid SessionId { get; set; }
    public Guid SubjectId { get; set; }
    public string Question { get; set; } = string.Empty;
}
