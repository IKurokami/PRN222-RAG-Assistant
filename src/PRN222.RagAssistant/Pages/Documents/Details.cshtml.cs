using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Models.Documents;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Pages.Documents;

[Authorize]
public class DetailsModel(
    ISubjectCatalogService subjectCatalogService,
    IChapterManagementService chapterManagementService,
    IDocumentManagementService documentManagementService,
    ISubjectAccessService subjectAccessService,
    UserManager<ApplicationUser> userManager) : PageModel
{
    private const int ChunkPreviewPageSize = 12;

    public bool CanManageDocuments { get; set; }
    public DocumentChunkPreviewPageViewModel ChunkPreview { get; set; } = new();
    public DocumentDetailViewModel Document { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(
        Guid id,
        int chunkPage = 1,
        CancellationToken cancellationToken = default)
    {
        var entity = await documentManagementService.GetDocumentAsync(id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        if (!await subjectAccessService.CanViewSubjectAsync(User, entity.SubjectId, cancellationToken))
        {
            return Forbid();
        }

        var subject = await subjectCatalogService.GetSubjectAsync(
            entity.SubjectId,
            cancellationToken: cancellationToken);

        if (subject is null)
        {
            return NotFound();
        }

        PRN222.RagAssistant.Domain.Entities.Chapter? chapter = null;
        if (entity.ChapterId.HasValue)
        {
            var candidate = await chapterManagementService.GetChapterAsync(
                entity.ChapterId.Value,
                cancellationToken);
            if (candidate?.SubjectId == entity.SubjectId)
            {
                chapter = candidate;
            }
        }

        var uploader = await userManager.FindByIdAsync(entity.UploadedByUserId.ToString());
        var chunkData = await documentManagementService.GetChunkPreviewAsync(
            id,
            chunkPage,
            ChunkPreviewPageSize,
            cancellationToken);

        CanManageDocuments = await subjectAccessService.CanManageSubjectAsync(
            User,
            entity.SubjectId,
            cancellationToken);

        ChunkPreview = new DocumentChunkPreviewPageViewModel
        {
            Items = chunkData.Items.Select(item => new DocumentChunkPreviewItemViewModel
            {
                ChunkIndex = item.ChunkIndex,
                Content = item.Content,
                PageNumber = item.PageNumber,
                SlideNumber = item.SlideNumber,
                HasEmbedding = item.HasEmbedding
            }).ToList(),
            TotalCount = chunkData.TotalCount,
            EmbeddedCount = chunkData.EmbeddedCount,
            CurrentPage = chunkData.CurrentPage,
            TotalPages = chunkData.TotalPages
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
            IndexedAtUtc = entity.IndexedAtUtc
        };

        return Page();
    }

    public async Task<IActionResult> OnPostReindexAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await documentManagementService.GetDocumentAsync(id, cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        if (!await subjectAccessService.CanManageSubjectAsync(User, document.SubjectId, cancellationToken))
        {
            return Forbid();
        }

        var updated = await documentManagementService.RequeueForIndexAsync(id, cancellationToken);
        if (updated is null)
        {
            return NotFound();
        }

        TempData["StatusMessage"] = $"Đã đưa tài liệu '{updated.Title}' vào hàng chờ index lại.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await documentManagementService.GetDocumentAsync(id, cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        if (!await subjectAccessService.CanManageSubjectAsync(User, document.SubjectId, cancellationToken))
        {
            return Forbid();
        }

        var deleted = await documentManagementService.DeleteDocumentAsync(id, cancellationToken);
        if (deleted is null)
        {
            return NotFound();
        }

        TempData["StatusMessage"] = $"Đã xóa tài liệu '{deleted.Title}' thành công.";
        return RedirectToPage("/Documents/Index", new { subjectId = deleted.SubjectId });
    }
}
