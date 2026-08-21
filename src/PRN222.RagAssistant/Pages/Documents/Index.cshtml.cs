using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Domain.Enums;
using PRN222.RagAssistant.Models.Documents;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Pages.Documents;

[Authorize]
public class IndexModel(
    ISubjectCatalogService subjectCatalogService,
    IChapterManagementService chapterManagementService,
    IDocumentManagementService documentManagementService,
    ISubjectAccessService subjectAccessService) : PageModel
{
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

        if (!await subjectAccessService.CanViewSubjectAsync(User, subjectId, cancellationToken))
        {
            return Forbid();
        }

        var subject = await subjectCatalogService.GetSubjectAsync(
            subjectId,
            cancellationToken: cancellationToken);

        if (subject is null)
        {
            return NotFound();
        }

        var chapters = await chapterManagementService.GetChaptersAsync(subjectId, cancellationToken);
        if (selectedChapterId.HasValue && chapters.All(chapter => chapter.Id != selectedChapterId.Value))
        {
            return BadRequest("The selected chapter does not belong to this subject.");
        }

        var normalizedSearchTerm = searchTerm?.Trim();
        var documents = await documentManagementService.GetDocumentsAsync(
            subjectId,
            selectedChapterId,
            normalizedSearchTerm,
            selectedStatus,
            cancellationToken);
        var totalDocumentCount = await documentManagementService.GetDocumentCountAsync(
            subjectId,
            cancellationToken);
        var chapterMap = chapters.ToDictionary(chapter => chapter.Id);

        SubjectId = subject.Id;
        SubjectCode = subject.Code;
        SubjectName = subject.Name;
        SelectedChapterId = selectedChapterId;
        SearchTerm = normalizedSearchTerm ?? string.Empty;
        SelectedStatus = selectedStatus;
        TotalDocumentCount = totalDocumentCount;
        CanManageDocuments = await subjectAccessService.CanManageSubjectAsync(
            User,
            subjectId,
            cancellationToken);
        StatusMessage = TempData["StatusMessage"] as string;
        ChapterOptions = chapters
            .Select(chapter => new SelectListItem
            {
                Value = chapter.Id.ToString(),
                Text = $"Chương {chapter.Number}: {chapter.Title}",
                Selected = chapter.Id == selectedChapterId
            })
            .ToList();

        Documents = documents.Select(document =>
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
        return RedirectToPage(new
        {
            subjectId = updated.SubjectId,
            selectedChapterId,
            searchTerm,
            selectedStatus
        });
    }

    public async Task<IActionResult> OnPostDeleteAsync(
        Guid id,
        Guid? selectedChapterId,
        string? searchTerm,
        DocumentIndexStatus? selectedStatus,
        CancellationToken cancellationToken)
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
        return RedirectToPage(new
        {
            subjectId = deleted.SubjectId,
            selectedChapterId,
            searchTerm,
            selectedStatus
        });
    }
}
