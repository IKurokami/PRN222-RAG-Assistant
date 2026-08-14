using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Data.Seed;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Domain.Enums;
using PRN222.RagAssistant.Models.Documents;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Controllers;

[Authorize]
public sealed class DocumentsController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IDocumentIndexingQueue _indexingQueue;
    private readonly IAuthorizationService _authorizationService;
    private readonly IConfiguration _configuration;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(
        ApplicationDbContext dbContext,
        IDocumentIndexingQueue indexingQueue,
        IAuthorizationService authorizationService,
        IConfiguration configuration,
        UserManager<ApplicationUser> userManager,
        ILogger<DocumentsController> logger)
    {
        _dbContext = dbContext;
        _indexingQueue = indexingQueue;
        _authorizationService = authorizationService;
        _configuration = configuration;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid? selectedChapterId, CancellationToken cancellationToken)
    {
        var authResult = await _authorizationService.AuthorizeAsync(User, AppPolicies.ManageDocuments);

        var chapters = await _dbContext.Chapters
            .AsNoTracking()
            .Where(c => c.SubjectId == SeedData.Prn222SubjectId)
            .OrderBy(c => c.Number)
            .ToListAsync(cancellationToken);

        var query = _dbContext.Documents
            .AsNoTracking()
            .Where(d => d.SubjectId == SeedData.Prn222SubjectId);

        if (selectedChapterId.HasValue)
        {
            query = query.Where(d => d.ChapterId == selectedChapterId.Value);
        }

        var documentEntities = await query
            .OrderByDescending(d => d.UploadedAtUtc)
            .ToListAsync(cancellationToken);

        var chapterMap = chapters.ToDictionary(c => c.Id);

        var viewModel = new DocumentIndexViewModel
        {
            SelectedChapterId = selectedChapterId,
            CanManageDocuments = authResult.Succeeded,
            StatusMessage = TempData["StatusMessage"] as string,
            ChapterOptions = chapters
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = $"Chương {c.Number}: {c.Title}",
                    Selected = c.Id == selectedChapterId
                })
                .ToList(),
            Documents = documentEntities.Select(d =>
            {
                chapterMap.TryGetValue(d.ChapterId ?? Guid.Empty, out var chapter);
                return new DocumentItemViewModel
                {
                    Id = d.Id,
                    Title = d.Title,
                    OriginalFileName = d.OriginalFileName,
                    ChapterNumber = chapter?.Number,
                    ChapterTitle = chapter?.Title,
                    FileSizeBytes = d.FileSizeBytes,
                    IndexStatus = d.IndexStatus,
                    IndexError = d.IndexError,
                    UploadedAtUtc = d.UploadedAtUtc,
                    IndexedAtUtc = d.IndexedAtUtc
                };
            }).ToList()
        };

        return View(viewModel);
    }

    [HttpGet]
    [Authorize(Policy = AppPolicies.ManageDocuments)]
    public async Task<IActionResult> Upload(CancellationToken cancellationToken)
    {
        var viewModel = new DocumentUploadViewModel();
        await LoadChapterOptionsAsync(viewModel.ChapterOptions, cancellationToken);
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.ManageDocuments)]
    public async Task<IActionResult> Upload(DocumentUploadViewModel viewModel, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadChapterOptionsAsync(viewModel.ChapterOptions, cancellationToken);
            return View(viewModel);
        }

        if (viewModel.Input.ChapterId.HasValue)
        {
            var chapterValid = await _dbContext.Chapters
                .AnyAsync(
                    c => c.Id == viewModel.Input.ChapterId.Value && c.SubjectId == SeedData.Prn222SubjectId,
                    cancellationToken);

            if (!chapterValid)
            {
                ModelState.AddModelError("Input.ChapterId", "Chương được chọn không hợp lệ hoặc không thuộc môn PRN222.");
                await LoadChapterOptionsAsync(viewModel.ChapterOptions, cancellationToken);
                return View(viewModel);
            }
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var file = viewModel.Input.File;
        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError("Input.File", "File tải lên không hợp lệ.");
            await LoadChapterOptionsAsync(viewModel.ChapterOptions, cancellationToken);
            return View(viewModel);
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
            SubjectId = SeedData.Prn222SubjectId,
            ChapterId = viewModel.Input.ChapterId,
            UploadedByUserId = user.Id,
            Title = viewModel.Input.Title.Trim(),
            OriginalFileName = file.FileName,
            StoragePath = storagePath,
            ContentType = file.ContentType ?? "application/octet-stream",
            FileExtension = extension,
            FileSizeBytes = file.Length,
            IndexStatus = DocumentIndexStatus.Uploaded,
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
            await LoadChapterOptionsAsync(viewModel.ChapterOptions, cancellationToken);
            return View(viewModel);
        }

        await _indexingQueue.EnqueueAsync(document.Id, cancellationToken);

        TempData["StatusMessage"] = $"Đã tải thành công tài liệu '{document.Title}'. File đã được thêm vào hàng chờ index.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var authResult = await _authorizationService.AuthorizeAsync(User, AppPolicies.ManageDocuments);

        var entity = await _dbContext.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (entity is null)
        {
            return NotFound();
        }

        var subject = await _dbContext.Subjects
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == entity.SubjectId, cancellationToken);

        var chapter = entity.ChapterId.HasValue
            ? await _dbContext.Chapters
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == entity.ChapterId.Value, cancellationToken)
            : null;

        var uploader = await _userManager.FindByIdAsync(entity.UploadedByUserId.ToString());

        var chunkCount = await _dbContext.DocumentChunks
            .AsNoTracking()
            .CountAsync(c => c.DocumentId == id, cancellationToken);

        var viewModel = new DocumentDetailsViewModel
        {
            CanManageDocuments = authResult.Succeeded,
            Document = new DocumentDetailViewModel
            {
                Id = entity.Id,
                Title = entity.Title,
                OriginalFileName = entity.OriginalFileName,
                StoragePath = entity.StoragePath,
                ContentType = entity.ContentType,
                FileExtension = entity.FileExtension,
                FileSizeBytes = entity.FileSizeBytes,
                SubjectCode = subject?.Code ?? "PRN222",
                SubjectName = subject?.Name ?? "C# & .NET Application Development",
                ChapterNumber = chapter?.Number,
                ChapterTitle = chapter?.Title,
                UploadedByEmail = uploader?.Email ?? "Hệ thống",
                IndexStatus = entity.IndexStatus,
                IndexError = entity.IndexError,
                UploadedAtUtc = entity.UploadedAtUtc,
                IndexedAtUtc = entity.IndexedAtUtc,
                ChunkCount = chunkCount
            }
        };

        return View(viewModel);
    }

    [HttpGet]
    [Authorize(Policy = AppPolicies.ManageDocuments)]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var document = await _dbContext.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (document is null)
        {
            return NotFound();
        }

        var viewModel = new DocumentEditViewModel
        {
            Id = document.Id,
            DocumentTitle = document.Title,
            Input = new DocumentEditInputModel
            {
                Title = document.Title,
                ChapterId = document.ChapterId
            }
        };

        await LoadChapterOptionsAsync(viewModel.ChapterOptions, cancellationToken);
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.ManageDocuments)]
    public async Task<IActionResult> Edit(Guid id, DocumentEditViewModel viewModel, CancellationToken cancellationToken)
    {
        var document = await _dbContext.Documents.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        viewModel.Id = id;
        viewModel.DocumentTitle = document.Title;

        if (!ModelState.IsValid)
        {
            await LoadChapterOptionsAsync(viewModel.ChapterOptions, cancellationToken);
            return View(viewModel);
        }

        if (viewModel.Input.ChapterId.HasValue)
        {
            var chapterValid = await _dbContext.Chapters
                .AnyAsync(
                    c => c.Id == viewModel.Input.ChapterId.Value && c.SubjectId == SeedData.Prn222SubjectId,
                    cancellationToken);

            if (!chapterValid)
            {
                ModelState.AddModelError("Input.ChapterId", "Chương được chọn không hợp lệ hoặc không thuộc môn PRN222.");
                await LoadChapterOptionsAsync(viewModel.ChapterOptions, cancellationToken);
                return View(viewModel);
            }
        }

        document.Title = viewModel.Input.Title.Trim();
        document.ChapterId = viewModel.Input.ChapterId;

        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["StatusMessage"] = $"Đã cập nhật thông tin tài liệu '{document.Title}' thành công.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.ManageDocuments)]
    public async Task<IActionResult> Delete(Guid id, Guid? selectedChapterId, CancellationToken cancellationToken)
    {
        var document = await _dbContext.Documents.FindAsync([id], cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

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
        return RedirectToAction(nameof(Index), new { selectedChapterId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.ManageDocuments)]
    public async Task<IActionResult> Reindex(Guid id, Guid? selectedChapterId, CancellationToken cancellationToken)
    {
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

        TempData["StatusMessage"] = $"Đã đưa tài liệu '{document.Title}' vào hàng chờ index lại.";
        return RedirectToAction(nameof(Index), new { selectedChapterId });
    }

    private async Task LoadChapterOptionsAsync(List<SelectListItem> target, CancellationToken cancellationToken)
    {
        var chapters = await _dbContext.Chapters
            .AsNoTracking()
            .Where(c => c.SubjectId == SeedData.Prn222SubjectId)
            .OrderBy(c => c.Number)
            .ToListAsync(cancellationToken);

        target.Clear();
        target.AddRange(chapters.Select(c => new SelectListItem
        {
            Value = c.Id.ToString(),
            Text = $"Chương {c.Number}: {c.Title}"
        }));
    }
}
