using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Domain.Enums;
using PRN222.RagAssistant.Models.Documents;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Pages.Documents;

[Authorize]
public class IndexModel : PageModel
{
    private const int ChunkPreviewPageSize = 12;

    private readonly ApplicationDbContext _dbContext;
    private readonly IDocumentIndexingQueue _indexingQueue;
    private readonly ISubjectAccessService _subjectAccessService;
    private readonly IConfiguration _configuration;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        ApplicationDbContext dbContext,
        IDocumentIndexingQueue indexingQueue,
        ISubjectAccessService subjectAccessService,
        IConfiguration configuration,
        UserManager<ApplicationUser> userManager,
        ILogger<IndexModel> logger)
    {
        _dbContext = dbContext;
        _indexingQueue = indexingQueue;
        _subjectAccessService = subjectAccessService;
        _configuration = configuration;
        _userManager = userManager;
        _logger = logger;
    }

    public Guid SubjectId { get; set; }
    public string SubjectCode { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public Guid? SelectedChapterId { get; set; }
    public string SearchTerm { get; set; } = string.Empty;
    public DocumentIndexStatus? SelectedStatus { get; set; }
    public int TotalDocumentCount { get; set; }
    public bool CanManageDocuments { get; set; }
    public string? StatusMessage { get; set; }
    public List<SelectListItem> ChapterOptions { get; set; } = new();
    public List<DocumentItemViewModel> Documents { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(
        Guid subjectId,
        Guid? selectedChapterId,
        string? searchTerm,
        DocumentIndexStatus? selectedStatus,
        CancellationToken cancellationToken)
    {
        if (subjectId == Guid.Empty)
        {
            return RedirectToPage("/Subjects/Index");
        }

        if (!await _subjectAccessService.CanViewSubjectAsync(User, subjectId, cancellationToken))
        {
            return Forbid();
        }

        var subject = await _dbContext.Subjects
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == subjectId, cancellationToken);

        if (subject is null)
        {
            return NotFound();
        }

        var chapters = await _dbContext.Chapters
            .AsNoTracking()
            .Where(chapter => chapter.SubjectId == subjectId)
            .OrderBy(chapter => chapter.Number)
            .ToListAsync(cancellationToken);

        if (selectedChapterId.HasValue && chapters.All(chapter => chapter.Id != selectedChapterId.Value))
        {
            return BadRequest("The selected chapter does not belong to this subject.");
        }

        var query = _dbContext.Documents
            .AsNoTracking()
            .Where(document => document.SubjectId == subjectId);

        var totalDocumentCount = await query.CountAsync(cancellationToken);

        if (selectedChapterId.HasValue)
        {
            query = query.Where(document => document.ChapterId == selectedChapterId.Value);
        }

        var normalizedSearchTerm = searchTerm?.Trim();
        if (!string.IsNullOrEmpty(normalizedSearchTerm))
        {
            var normalizedSearch = normalizedSearchTerm.ToLowerInvariant();
            query = query.Where(document =>
                document.Title.ToLower().Contains(normalizedSearch)
                || document.OriginalFileName.ToLower().Contains(normalizedSearch));
        }

        if (selectedStatus.HasValue)
        {
            query = query.Where(document => document.IndexStatus == selectedStatus.Value);
        }

        var documentEntities = await query
            .OrderByDescending(document => document.UploadedAtUtc)
            .ToListAsync(cancellationToken);

        var chapterMap = chapters.ToDictionary(chapter => chapter.Id);

        SubjectId = subject.Id;
        SubjectCode = subject.Code;
        SubjectName = subject.Name;
        SelectedChapterId = selectedChapterId;
        SearchTerm = normalizedSearchTerm ?? string.Empty;
        SelectedStatus = selectedStatus;
        TotalDocumentCount = totalDocumentCount;
        CanManageDocuments = await _subjectAccessService.CanManageSubjectAsync(User, subjectId, cancellationToken);
        StatusMessage = TempData["StatusMessage"] as string;
        ChapterOptions = chapters
            .Select(chapter => new SelectListItem
            {
                Value = chapter.Id.ToString(),
                Text = $"Chương {chapter.Number}: {chapter.Title}",
                Selected = chapter.Id == selectedChapterId
            })
            .ToList();
        Documents = documentEntities.Select(document =>
        {
            chapterMap.TryGetValue(document.ChapterId ?? Guid.Empty, out var chapter);
            return new DocumentItemViewModel
            {
                Id = document.Id,
                Title = document.Title,
                OriginalFileName = document.OriginalFileName,
                ChapterNumber = chapter?.Number,
                ChapterTitle = chapter?.Title,
                FileSizeBytes = document.FileSizeBytes,
                IndexStatus = document.IndexStatus,
                IndexError = document.IndexError,
                UploadedAtUtc = document.UploadedAtUtc,
                IndexedAtUtc = document.IndexedAtUtc
            };
        }).ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostReindexAsync(
        Guid id,
        Guid? selectedChapterId,
        string? searchTerm,
        DocumentIndexStatus? selectedStatus,
        CancellationToken cancellationToken)
    {
        var document = await _dbContext.Documents.FindAsync([id], cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        if (!await _subjectAccessService.CanManageSubjectAsync(User, document.SubjectId, cancellationToken))
        {
            return Forbid();
        }

        document.IndexStatus = DocumentIndexStatus.Uploaded;
        document.IndexError = null;
        document.IndexedAtUtc = null;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _indexingQueue.EnqueueAsync(document.Id, cancellationToken);

        TempData["StatusMessage"] = $"Đã đưa tài liệu '{document.Title}' vào hàng chờ index lại.";
        return RedirectToPage(new { subjectId = document.SubjectId, selectedChapterId, searchTerm, selectedStatus });
    }

    public async Task<IActionResult> OnPostDeleteAsync(
        Guid id,
        Guid? selectedChapterId,
        string? searchTerm,
        DocumentIndexStatus? selectedStatus,
        CancellationToken cancellationToken)
    {
        var document = await _dbContext.Documents.FindAsync([id], cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        if (!await _subjectAccessService.CanManageSubjectAsync(User, document.SubjectId, cancellationToken))
        {
            return Forbid();
        }

        var subjectId = document.SubjectId;
        var storagePathToDelete = document.StoragePath;

        _dbContext.Documents.Remove(document);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (System.IO.File.Exists(storagePathToDelete))
        {
            try
            {
                System.IO.File.Delete(storagePathToDelete);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Document record {DocumentId} deleted from DB but failed to remove physical file at {StoragePath}.",
                    id,
                    storagePathToDelete);
            }
        }

        TempData["StatusMessage"] = $"Đã xóa tài liệu '{document.Title}' thành công.";
        return RedirectToPage(new { subjectId, selectedChapterId, searchTerm, selectedStatus });
    }
}
