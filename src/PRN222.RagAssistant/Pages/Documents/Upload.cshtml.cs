using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Models.Documents;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Pages.Documents;

[Authorize(Policy = AppPolicies.ManageDocuments)]
public class UploadModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IDocumentIndexingQueue _indexingQueue;
    private readonly ISubjectAccessService _subjectAccessService;
    private readonly IConfiguration _configuration;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<UploadModel> _logger;

    public UploadModel(
        ApplicationDbContext dbContext,
        IDocumentIndexingQueue indexingQueue,
        ISubjectAccessService subjectAccessService,
        IConfiguration configuration,
        UserManager<ApplicationUser> userManager,
        ILogger<UploadModel> logger)
    {
        _dbContext = dbContext;
        _indexingQueue = indexingQueue;
        _subjectAccessService = subjectAccessService;
        _configuration = configuration;
        _userManager = userManager;
        _logger = logger;
    }

    [BindProperty]
    public Guid SubjectId { get; set; }

    public string SubjectCode { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public List<SelectListItem> ChapterOptions { get; set; } = new();

    [BindProperty]
    public DocumentUploadInputModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        if (!await _subjectAccessService.CanManageSubjectAsync(User, subjectId, cancellationToken))
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

        SubjectId = subjectId;
        SubjectCode = subject.Code;
        SubjectName = subject.Name;
        await LoadChapterOptionsAsync(subjectId, cancellationToken);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!await _subjectAccessService.CanManageSubjectAsync(User, SubjectId, cancellationToken))
        {
            return Forbid();
        }

        var subject = await _dbContext.Subjects
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == SubjectId, cancellationToken);

        if (subject is null)
        {
            return NotFound();
        }

        SubjectCode = subject.Code;
        SubjectName = subject.Name;
        await LoadChapterOptionsAsync(SubjectId, cancellationToken);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (Input.ChapterId.HasValue)
        {
            var chapterValid = await _dbContext.Chapters.AnyAsync(
                chapter => chapter.Id == Input.ChapterId.Value
                           && chapter.SubjectId == SubjectId,
                cancellationToken);

            if (!chapterValid)
            {
                ModelState.AddModelError("Input.ChapterId", "Chương được chọn không hợp lệ hoặc không thuộc môn học này.");
                return Page();
            }
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var file = Input.File;
        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError("Input.File", "File tải lên không hợp lệ.");
            return Page();
        }

        var uploadsFolderSetting = _configuration["Rag:Storage:UploadsPath"] ?? "storage/uploads";
        var uploadsFolder = Path.IsPathRooted(uploadsFolderSetting)
            ? uploadsFolderSetting
            : Path.Combine(Directory.GetCurrentDirectory(), uploadsFolderSetting);

        Directory.CreateDirectory(uploadsFolder);

        var documentId = Guid.NewGuid();
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var storagePath = Path.Combine(uploadsFolder, $"{documentId}{extension}").Replace('\\', '/');

        await using (var stream = new FileStream(storagePath, FileMode.Create))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        var document = new Document
        {
            Id = documentId,
            SubjectId = SubjectId,
            ChapterId = Input.ChapterId,
            UploadedByUserId = user.Id,
            Title = Input.Title.Trim(),
            OriginalFileName = file.FileName,
            StoragePath = storagePath,
            ContentType = file.ContentType ?? "application/octet-stream",
            FileExtension = extension,
            FileSizeBytes = file.Length,
            IndexStatus = Domain.Enums.DocumentIndexStatus.Uploaded,
            UploadedAtUtc = DateTime.UtcNow
        };

        try
        {
            _dbContext.Documents.Add(document);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to persist document record for {DocumentId}. Cleaning up uploaded file at {StoragePath}.",
                documentId,
                storagePath);

            try
            {
                if (System.IO.File.Exists(storagePath))
                {
                    System.IO.File.Delete(storagePath);
                }
            }
            catch (Exception cleanupEx)
            {
                _logger.LogWarning(cleanupEx, "Failed to clean up orphan file at {StoragePath}.", storagePath);
            }

            ModelState.AddModelError(string.Empty, "Lưu thông tin tài liệu thất bại. Vui lòng thử lại.");
            return Page();
        }

        await _indexingQueue.EnqueueAsync(document.Id, cancellationToken);

        TempData["StatusMessage"] = $"Đã tải thành công tài liệu '{document.Title}'. File đã được thêm vào hàng chờ index.";
        return RedirectToPage("/Documents/Index", new { subjectId = document.SubjectId });
    }

    private async Task LoadChapterOptionsAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        var chapters = await _dbContext.Chapters
            .AsNoTracking()
            .Where(chapter => chapter.SubjectId == subjectId)
            .OrderBy(chapter => chapter.Number)
            .ToListAsync(cancellationToken);

        ChapterOptions = chapters.Select(chapter => new SelectListItem
        {
            Value = chapter.Id.ToString(),
            Text = $"Chương {chapter.Number}: {chapter.Title}"
        }).ToList();
    }
}
