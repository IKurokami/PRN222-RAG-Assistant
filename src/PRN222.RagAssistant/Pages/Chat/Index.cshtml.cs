using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Application.Models;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Infrastructure.Rag.Exceptions;

namespace PRN222.RagAssistant.Pages.Chat;

[Authorize]
public sealed class IndexModel : PageModel
{
    private readonly IRagQueryService _ragQueryService;
    private readonly IChatPageService _chatPageService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        IRagQueryService ragQueryService,
        IChatPageService chatPageService,
        UserManager<ApplicationUser> userManager,
        ILogger<IndexModel> logger)
    {
        _ragQueryService = ragQueryService;
        _chatPageService = chatPageService;
        _userManager = userManager;
        _logger = logger;
    }

    public IReadOnlyList<ChatSubjectItem> Subjects { get; private set; } = Array.Empty<ChatSubjectItem>();
    public Guid SelectedSubjectId { get; private set; }
    public string SelectedSubjectName { get; private set; } = string.Empty;
    public string SelectedSubjectCode { get; private set; } = string.Empty;
    public IReadOnlyList<ChatSessionItem> Sessions { get; private set; } = Array.Empty<ChatSessionItem>();
    public ChatSessionItem? ActiveSession { get; private set; }
    public IReadOnlyList<ChatMessageItem> Messages { get; private set; } = Array.Empty<ChatMessageItem>();
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

        var page = await _chatPageService.GetPageAsync(
            user.Id,
            subjectId,
            sessionId,
            cancellationToken);

        Subjects = page.Subjects;
        Sessions = page.Sessions;
        ActiveSession = page.ActiveSession;
        Messages = page.Messages;

        if (page.SelectedSubject == null)
        {
            ErrorMessage = "Chưa có Môn học nào được kích hoạt trong hệ thống.";
            return Page();
        }

        SelectedSubjectId = page.SelectedSubject.Id;
        SelectedSubjectName = page.SelectedSubject.Name;
        SelectedSubjectCode = page.SelectedSubject.Code;

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
        catch (InsufficientQuotaException ex)
        {
            _logger.LogWarning("Chat request blocked due to insufficient quota for User {UserId}", user.Id);
            return new JsonResult(new { action = "insufficient_quota", message = ex.Message })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
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
        Response.Headers["X-Accel-Buffering"] = "no";
        Response.HttpContext.Features
            .Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>()?
            .DisableBuffering();
        await Response.StartAsync(cancellationToken);

        async Task SendEventAsync(string eventName, object data)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(data);
            await Response.WriteAsync($"event: {eventName}\ndata: {json}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }

        async Task SendHeartbeatAsync()
        {
            await Response.WriteAsync(": keep-alive\n\n", cancellationToken);
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
            // The RAG service now owns retrieval/tool/model streaming. This handler only
            // translates typed application events to SSE and never fabricates token deltas.
            await using var stream = _ragQueryService.AskStreamAsync(
                    user.Id,
                    dto.SessionId,
                    dto.Question,
                    dto.SubjectId,
                    cancellationToken)
                .GetAsyncEnumerator(cancellationToken);

            while (true)
            {
                var moveNextTask = stream.MoveNextAsync().AsTask();

                // Retrieval tools and model first-token latency can exceed proxy idle
                // windows, so keep the SSE socket alive without inventing progress data.
                while (!moveNextTask.IsCompleted)
                {
                    var completedTask = await Task.WhenAny(
                        moveNextTask,
                        Task.Delay(TimeSpan.FromSeconds(10), cancellationToken));

                    if (completedTask == moveNextTask)
                    {
                        break;
                    }

                    await SendHeartbeatAsync();
                }

                if (!await moveNextTask)
                {
                    break;
                }

                switch (stream.Current)
                {
                    case RagToolCallEvent toolCall:
                        await SendEventAsync("tool_call", new
                        {
                            id = toolCall.Id,
                            tool = toolCall.Tool,
                            status = toolCall.Status,
                            title = toolCall.Title,
                            detail = toolCall.Detail
                        });
                        break;

                    case RagDeltaEvent delta:
                        // Each delta is a real provider/model streaming update.
                        await SendEventAsync("delta", new { content = delta.Content });
                        break;

                    case RagCitationsEvent citationsEvent:
                        await SendEventAsync("citations", new
                        {
                            citations = citationsEvent.Citations.Select(c => new
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
                        break;

                    case RagCompletedEvent completed:
                        await SendEventAsync("done", new
                        {
                            answer = completed.Answer.Answer,
                            citations = completed.Answer.Citations.Select(c => new
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
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("Chat stream cancelled for User {UserId}", user.Id);
        }
        catch (InsufficientQuotaException ex)
        {
            _logger.LogWarning("Chat stream blocked due to insufficient quota for User {UserId}", user.Id);
            try
            {
                await SendEventAsync("error", new
                {
                    action = "insufficient_quota",
                    message = ex.Message
                });
            }
            catch (Exception sendError)
            {
                _logger.LogDebug(
                    sendError,
                    "Unable to send quota error event for User {UserId}",
                    user.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat stream request failed for User {UserId}", user.Id);

            try
            {
                await SendEventAsync("error", new { message = "Đã xảy ra lỗi: " + ex.Message });
            }
            catch (Exception sendError)
            {
                _logger.LogDebug(
                    sendError,
                    "Unable to send chat stream error event because the client connection is no longer available for User {UserId}",
                    user.Id);
            }
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

        var sessionId = await _chatPageService.CreateSessionAsync(
            user.Id,
            subjectId,
            cancellationToken);

        return RedirectToPage("/Chat/Index", new { subjectId, sessionId });
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

        await _chatPageService.DeleteSessionAsync(
            user.Id,
            sessionId,
            cancellationToken);

        return RedirectToPage("/Chat/Index", new { subjectId });
    }
}

public sealed class AskRequestDto
{
    public Guid SessionId { get; set; }
    public Guid SubjectId { get; set; }
    public string Question { get; set; } = string.Empty;
}
