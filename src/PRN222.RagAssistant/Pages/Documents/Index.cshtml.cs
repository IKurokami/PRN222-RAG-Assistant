using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Data.Seed;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Domain.Enums;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Pages.Documents;

[Authorize]
public sealed class IndexModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IDocumentIndexingQueue _indexingQueue;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        ApplicationDbContext dbContext,
        IDocumentIndexingQueue indexingQueue,
        IAuthorizationService authorizationService,
        ILogger<IndexModel> logger)
    {
        _dbContext = dbContext;
        _indexingQueue = indexingQueue;
        _authorizationService = authorizationService;
        _logger = logger;
    }

    public List<DocumentItemViewModel> Documents { get; set; } = [];
    public List<SelectListItem> ChapterOptions { get; set; } = [];

    [BindProperty(SupportsGet = true)]
    public Guid? SelectedChapterId { get; set; }

    public bool CanManageDocuments { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var authResult = await _authorizationService.AuthorizeAsync(User, AppPolicies.ManageDocuments);
        CanManageDocuments = authResult.Succeeded;

        var chapters = await _dbContext.Chapters
            .Where(c => c.SubjectId == SeedData.Prn222SubjectId)
            .OrderBy(c => c.Number)
            .ToListAsync(cancellationToken);

        ChapterOptions = chapters
            .Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = $"Chương {c.Number}: {c.Title}",
                Selected = c.Id == SelectedChapterId
            })
            .ToList();

        var query = _dbContext.Documents
            .Where(d => d.SubjectId == SeedData.Prn222SubjectId);

        if (SelectedChapterId.HasValue)
        {
            query = query.Where(d => d.ChapterId == SelectedChapterId.Value);
        }

        var documentEntities = await query
            .OrderByDescending(d => d.UploadedAtUtc)
            .ToListAsync(cancellationToken);

        var chapterMap = chapters.ToDictionary(c => c.Id);

        Documents = documentEntities.Select(d =>
        {
            chapterMap.TryGetValue(d.ChapterId ?? Guid.Empty, out var ch);
            return new DocumentItemViewModel
            {
                Id = d.Id,
                Title = d.Title,
                OriginalFileName = d.OriginalFileName,
                ChapterNumber = ch?.Number,
                ChapterTitle = ch?.Title,
                FileSizeBytes = d.FileSizeBytes,
                IndexStatus = d.IndexStatus,
                IndexError = d.IndexError,
                UploadedAtUtc = d.UploadedAtUtc,
                IndexedAtUtc = d.IndexedAtUtc
            };
        }).ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var authResult = await _authorizationService.AuthorizeAsync(User, AppPolicies.ManageDocuments);
        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var document = await _dbContext.Documents.FindAsync([id], cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        var storagePathToDelete = document.StoragePath;

        // Blocker 4: Commit DB trước để bảo toàn consistency.
        // Nếu DB commit thành công mà file cleanup thất bại thì chỉ log warning,
        // không để lại record trong DB trỏ đến file đã mất.
        _dbContext.Documents.Remove(document);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Cleanup physical file sau khi DB commit thành công
        if (System.IO.File.Exists(storagePathToDelete))
        {
            try
            {
                System.IO.File.Delete(storagePathToDelete);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Document record {DocumentId} deleted from DB but failed to remove physical file at {StoragePath}.", id, storagePathToDelete);
            }
        }

        StatusMessage = $"Đã xóa tài liệu '{document.Title}' thành công.";
        return RedirectToPage(new { SelectedChapterId });
    }

    public async Task<IActionResult> OnPostReindexAsync(Guid id, CancellationToken cancellationToken)
    {
        var authResult = await _authorizationService.AuthorizeAsync(User, AppPolicies.ManageDocuments);
        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var document = await _dbContext.Documents.FindAsync([id], cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        document.IndexStatus = DocumentIndexStatus.Uploaded;
        document.IndexError = null;
        document.IndexedAtUtc = null;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _indexingQueue.EnqueueAsync(document.Id, cancellationToken);

        StatusMessage = $"Đã đưa tài liệu '{document.Title}' vào hàng chờ index lại.";
        return RedirectToPage(new { SelectedChapterId });
    }

    public sealed class DocumentItemViewModel
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public int? ChapterNumber { get; set; }
        public string? ChapterTitle { get; set; }
        public long FileSizeBytes { get; set; }
        public DocumentIndexStatus IndexStatus { get; set; }
        public string? IndexError { get; set; }
        public DateTime UploadedAtUtc { get; set; }
        public DateTime? IndexedAtUtc { get; set; }

        public string FormattedSize => FileSizeBytes switch
        {
            < 1024 => $"{FileSizeBytes} B",
            < 1024 * 1024 => $"{FileSizeBytes / 1024.0:F1} KB",
            _ => $"{FileSizeBytes / (1024.0 * 1024.0):F1} MB"
        };
    }
}
