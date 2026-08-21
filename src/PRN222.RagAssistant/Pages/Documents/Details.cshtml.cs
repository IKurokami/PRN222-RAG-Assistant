using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Domain.Enums;
using PRN222.RagAssistant.Models.Documents;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Pages.Documents;

[Authorize]
public class DetailsModel : PageModel
{
    private const int ChunkPreviewPageSize = 12;

    private readonly ApplicationDbContext _dbContext;
    private readonly IDocumentIndexingQueue _indexingQueue;
    private readonly ISubjectAccessService _subjectAccessService;
    private readonly UserManager<ApplicationUser> _userManager;

    public DetailsModel(
        ApplicationDbContext dbContext,
        IDocumentIndexingQueue indexingQueue,
        ISubjectAccessService subjectAccessService,
        UserManager<ApplicationUser> userManager)
    {
        _dbContext = dbContext;
        _indexingQueue = indexingQueue;
        _subjectAccessService = subjectAccessService;
        _userManager = userManager;
    }

    public bool CanManageDocuments { get; set; }
    public DocumentChunkPreviewPageViewModel ChunkPreview { get; set; } = new();
    public DocumentDetailViewModel Document { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(
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

        CanManageDocuments = await _subjectAccessService.CanManageSubjectAsync(User, entity.SubjectId, cancellationToken);
        ChunkPreview = new DocumentChunkPreviewPageViewModel
        {
            Items = chunkPreviewItems,
            TotalCount = chunkCount,
            EmbeddedCount = embeddedChunkCount,
            CurrentPage = currentChunkPage,
            TotalPages = totalChunkPages
        };
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
        };

        return Page();
    }

    public async Task<IActionResult> OnPostReindexAsync(Guid id, CancellationToken cancellationToken)
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
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken cancellationToken)
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

        _dbContext.Documents.Remove(document);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (System.IO.File.Exists(document.StoragePath))
        {
            try
            {
                System.IO.File.Delete(document.StoragePath);
            }
            catch
            {
                // Silently fail
            }
        }

        TempData["StatusMessage"] = $"Đã xóa tài liệu '{document.Title}' thành công.";
        return RedirectToPage("/Documents/Index", new { subjectId });
    }
}
