using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Domain.Enums;
using PRN222.RagAssistant.Models.Documents;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Controllers;

[Authorize]
public sealed class DocumentsController : Controller
{
    private const int ChunkPreviewPageSize = 12;

    private readonly ApplicationDbContext _dbContext;
    private readonly IDocumentIndexingQueue _indexingQueue;
    private readonly ISubjectAccessService _subjectAccessService;
    private readonly IConfiguration _configuration;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(
        ApplicationDbContext dbContext,
        IDocumentIndexingQueue indexingQueue,
        ISubjectAccessService subjectAccessService,
        IConfiguration configuration,
        UserManager<ApplicationUser> userManager,
        ILogger<DocumentsController> logger)
    {
        _dbContext = dbContext;
        _indexingQueue = indexingQueue;
        _subjectAccessService = subjectAccessService;
        _configuration = configuration;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        Guid subjectId,
        Guid? selectedChapterId,
        string? searchTerm,
        DocumentIndexStatus? selectedStatus,
        CancellationToken cancellationToken)
    {
        if (subjectId == Guid.Empty)
        {
            return RedirectToAction(nameof(SubjectsController.Index), "Subjects");
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

        var viewModel = new DocumentIndexViewModel
        {
            SubjectId = subject.Id,
            SubjectCode = subject.Code,
            SubjectName = subject.Name,
            SelectedChapterId = selectedChapterId,
            SearchTerm = normalizedSearchTerm ?? string.Empty,
            SelectedStatus = selectedStatus,
            TotalDocumentCount = totalDocumentCount,
            CanManageDocuments = await _subjectAccessService.CanManageSubjectAsync(User, subjectId, cancellationToken),
            StatusMessage = TempData["StatusMessage"] as string,
            ChapterOptions = chapters
                .Select(chapter => new SelectListItem
                {
                    Value = chapter.Id.ToString(),
                    Text = $"Chương {chapter.Number}: {chapter.Title}",
                    Selected = chapter.Id == selectedChapterId
                })
                .ToList(),
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
            }).ToList()
        };

        return View(viewModel);
    }

    [HttpGet]
    [Authorize(Policy = AppPolicies.ManageDocuments)]
    public async Task<IActionResult> Upload(Guid subjectId, CancellationToken cancellationToken)
    {
        if (!await _subjectAccessService.CanManageSubjectAsync(User, subjectId, cancellationToken))
        {
            return Forbid();
        }

        var viewModel = new DocumentUploadViewModel { SubjectId = subjectId };
        if (!await PopulateUploadMetadataAsync(viewModel, cancellationToken))
        {
            return NotFound();
        }

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.ManageDocuments)]
    public async Task<IActionResult> Upload(
        DocumentUploadViewModel viewModel,
        CancellationToken cancellationToken)
    {
        if (!await _subjectAccessService.CanManageSubjectAsync(User, viewModel.SubjectId, cancellationToken))
        {
            return Forbid();
        }

        if (!await PopulateUploadMetadataAsync(viewModel, cancellationToken))
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(viewModel);
        }

        if (viewModel.Input.ChapterId.HasValue)
        {
            var chapterValid = await _dbContext.Chapters.AnyAsync(
                chapter => chapter.Id == viewModel.Input.ChapterId.Value
                           && chapter.SubjectId == viewModel.SubjectId,
                cancellationToken);

            if (!chapterValid)
            {
                ModelState.AddModelError("Input.ChapterId", "Chương được chọn không hợp lệ hoặc không thuộc môn học này.");
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
            SubjectId = viewModel.SubjectId,
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
            return View(viewModel);
        }

        await _indexingQueue.EnqueueAsync(document.Id, cancellationToken);

        TempData["StatusMessage"] = $"Đã tải thành công tài liệu '{document.Title}'. File đã được thêm vào hàng chờ index.";
        return RedirectToAction(nameof(Index), new { subjectId = document.SubjectId });
    }

    [HttpGet]
    public async Task<IActionResult> Details(
        Guid id,
        int chunkPage = 1,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(document => document.Id == id, cancellationToken);

        if (entity is null)
        {
            return NotFound();
        }

        if (!await _subjectAccessService.CanViewSubjectAsync(User, entity.SubjectId, cancellationToken))
        {
            return Forbid();
        }

        var subject = await _dbContext.Subjects
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == entity.SubjectId, cancellationToken);

        if (subject is null)
        {
            return NotFound();
        }

        var chapter = entity.ChapterId.HasValue
            ? await _dbContext.Chapters
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    candidate => candidate.Id == entity.ChapterId.Value && candidate.SubjectId == entity.SubjectId,
                    cancellationToken)
            : null;

        var uploader = await _userManager.FindByIdAsync(entity.UploadedByUserId.ToString());
        var chunkQuery = _dbContext.DocumentChunks
            .AsNoTracking()
            .Where(chunk => chunk.DocumentId == id);
        var chunkCount = await chunkQuery.CountAsync(cancellationToken);
        var embeddedChunkCount = chunkCount == 0
            ? 0
            : await chunkQuery.CountAsync(chunk => chunk.Embedding != null, cancellationToken);
        var totalChunkPages = chunkCount == 0
            ? 0
            : ((chunkCount - 1) / ChunkPreviewPageSize) + 1;
        var currentChunkPage = totalChunkPages == 0
            ? 1
            : Math.Clamp(chunkPage, 1, totalChunkPages);

        List<DocumentChunkPreviewItemViewModel> chunkPreviewItems = [];
        if (chunkCount > 0)
        {
            chunkPreviewItems = await chunkQuery
                .OrderBy(chunk => chunk.ChunkIndex)
                .Skip((currentChunkPage - 1) * ChunkPreviewPageSize)
                .Take(ChunkPreviewPageSize)
                .Select(chunk => new DocumentChunkPreviewItemViewModel
                {
                    ChunkIndex = chunk.ChunkIndex,
                    Content = chunk.Content,
                    PageNumber = chunk.PageNumber,
                    SlideNumber = chunk.SlideNumber,
                    HasEmbedding = chunk.Embedding != null
                })
                .ToListAsync(cancellationToken);
        }

        return View(new DocumentDetailsViewModel
        {
            CanManageDocuments = await _subjectAccessService.CanManageSubjectAsync(User, entity.SubjectId, cancellationToken),
            ChunkPreview = new DocumentChunkPreviewPageViewModel
            {
                Items = chunkPreviewItems,
                TotalCount = chunkCount,
                EmbeddedCount = embeddedChunkCount,
                CurrentPage = currentChunkPage,
                TotalPages = totalChunkPages
            },
            Document = new DocumentDetailViewModel
            {
                Id = entity.Id,
                SubjectId = entity.SubjectId,
                Title = entity.Title,
                OriginalFileName = entity.OriginalFileName,
                StoragePath = entity.StoragePath,
                ContentType = entity.ContentType,
                FileExtension = entity.FileExtension,
                FileSizeBytes = entity.FileSizeBytes,
                SubjectCode = subject.Code,
                SubjectName = subject.Name,
                ChapterNumber = chapter?.Number,
                ChapterTitle = chapter?.Title,
                UploadedByEmail = uploader?.Email ?? "Hệ thống",
                IndexStatus = entity.IndexStatus,
                IndexError = entity.IndexError,
                UploadedAtUtc = entity.UploadedAtUtc,
                IndexedAtUtc = entity.IndexedAtUtc,
            }
        });
    }

    [HttpGet]
    [Authorize(Policy = AppPolicies.ManageDocuments)]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var document = await _dbContext.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (document is null)
        {
            return NotFound();
        }

        if (!await _subjectAccessService.CanManageSubjectAsync(User, document.SubjectId, cancellationToken))
        {
            return Forbid();
        }

        var viewModel = new DocumentEditViewModel
        {
            Id = document.Id,
            SubjectId = document.SubjectId,
            DocumentTitle = document.Title,
            Input = new DocumentEditInputModel
            {
                Title = document.Title,
                ChapterId = document.ChapterId
            }
        };

        if (!await PopulateEditMetadataAsync(viewModel, cancellationToken))
        {
            return NotFound();
        }

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.ManageDocuments)]
    public async Task<IActionResult> Edit(
        Guid id,
        DocumentEditViewModel viewModel,
        CancellationToken cancellationToken)
    {
        var document = await _dbContext.Documents.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        if (!await _subjectAccessService.CanManageSubjectAsync(User, document.SubjectId, cancellationToken))
        {
            return Forbid();
        }

        viewModel.Id = id;
        viewModel.SubjectId = document.SubjectId;
        viewModel.DocumentTitle = document.Title;
        if (!await PopulateEditMetadataAsync(viewModel, cancellationToken))
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(viewModel);
        }

        if (viewModel.Input.ChapterId.HasValue)
        {
            var chapterValid = await _dbContext.Chapters.AnyAsync(
                chapter => chapter.Id == viewModel.Input.ChapterId.Value
                           && chapter.SubjectId == document.SubjectId,
                cancellationToken);

            if (!chapterValid)
            {
                ModelState.AddModelError("Input.ChapterId", "Chương được chọn không hợp lệ hoặc không thuộc môn học này.");
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
    public async Task<IActionResult> Delete(
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
        return RedirectToAction(nameof(Index), new { subjectId, selectedChapterId, searchTerm, selectedStatus });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.ManageDocuments)]
    public async Task<IActionResult> Reindex(
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
        return RedirectToAction(
            nameof(Index),
            new { subjectId = document.SubjectId, selectedChapterId, searchTerm, selectedStatus });
    }

    private async Task<bool> PopulateUploadMetadataAsync(
        DocumentUploadViewModel viewModel,
        CancellationToken cancellationToken)
    {
        var subject = await _dbContext.Subjects
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == viewModel.SubjectId, cancellationToken);

        if (subject is null)
        {
            return false;
        }

        viewModel.SubjectCode = subject.Code;
        viewModel.SubjectName = subject.Name;
        await LoadChapterOptionsAsync(viewModel.SubjectId, viewModel.ChapterOptions, cancellationToken);
        return true;
    }

    private async Task<bool> PopulateEditMetadataAsync(
        DocumentEditViewModel viewModel,
        CancellationToken cancellationToken)
    {
        var subject = await _dbContext.Subjects
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == viewModel.SubjectId, cancellationToken);

        if (subject is null)
        {
            return false;
        }

        viewModel.SubjectCode = subject.Code;
        viewModel.SubjectName = subject.Name;
        await LoadChapterOptionsAsync(viewModel.SubjectId, viewModel.ChapterOptions, cancellationToken);
        return true;
    }

    private async Task LoadChapterOptionsAsync(
        Guid subjectId,
        List<SelectListItem> target,
        CancellationToken cancellationToken)
    {
        var chapters = await _dbContext.Chapters
            .AsNoTracking()
            .Where(chapter => chapter.SubjectId == subjectId)
            .OrderBy(chapter => chapter.Number)
            .ToListAsync(cancellationToken);

        target.Clear();
        target.AddRange(chapters.Select(chapter => new SelectListItem
        {
            Value = chapter.Id.ToString(),
            Text = $"Chương {chapter.Number}: {chapter.Title}"
        }));
    }
}
