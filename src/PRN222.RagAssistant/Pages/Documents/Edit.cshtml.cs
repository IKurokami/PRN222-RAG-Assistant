using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Models.Documents;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Pages.Documents;

[Authorize(Policy = AppPolicies.ManageDocuments)]
public class EditModel(
    ISubjectCatalogService subjectCatalogService,
    IChapterManagementService chapterManagementService,
    IDocumentManagementService documentManagementService,
    ISubjectAccessService subjectAccessService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid SubjectId { get; set; }

    public string SubjectCode { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public string DocumentTitle { get; set; } = string.Empty;
    public List<SelectListItem> ChapterOptions { get; set; } = new();

    [BindProperty]
    public DocumentEditInputModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
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

        ApplyDocument(document);
        Input = new DocumentEditInputModel
        {
            Title = document.Title,
            ChapterId = document.ChapterId
        };

        if (!await PopulateMetadataAsync(cancellationToken))
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var document = await documentManagementService.GetDocumentAsync(Id, cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        if (!await subjectAccessService.CanManageSubjectAsync(User, document.SubjectId, cancellationToken))
        {
            return Forbid();
        }

        ApplyDocument(document);
        if (!await PopulateMetadataAsync(cancellationToken))
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (Input.ChapterId.HasValue
            && ChapterOptions.All(option => option.Value != Input.ChapterId.Value.ToString()))
        {
            ModelState.AddModelError(
                "Input.ChapterId",
                "Chương được chọn không hợp lệ hoặc không thuộc môn học này.");
            return Page();
        }

        PRN222.RagAssistant.Domain.Entities.Document? updated;
        try
        {
            updated = await documentManagementService.UpdateDocumentAsync(
                Id,
                Input.Title.Trim(),
                Input.ChapterId,
                cancellationToken);
        }
        catch (InvalidOperationException)
        {
            ModelState.AddModelError(
                "Input.ChapterId",
                "Chương được chọn không hợp lệ hoặc không thuộc môn học này.");
            return Page();
        }

        if (updated is null)
        {
            return NotFound();
        }

        TempData["StatusMessage"] = $"Đã cập nhật thông tin tài liệu '{updated.Title}' thành công.";
        return RedirectToPage("/Documents/Details", new { id = Id });
    }

    private void ApplyDocument(PRN222.RagAssistant.Domain.Entities.Document document)
    {
        Id = document.Id;
        SubjectId = document.SubjectId;
        DocumentTitle = document.Title;
    }

    private async Task<bool> PopulateMetadataAsync(CancellationToken cancellationToken)
    {
        var subject = await subjectCatalogService.GetSubjectAsync(
            SubjectId,
            cancellationToken: cancellationToken);

        if (subject is null)
        {
            return false;
        }

        SubjectCode = subject.Code;
        SubjectName = subject.Name;

        var chapters = await chapterManagementService.GetChaptersAsync(SubjectId, cancellationToken);
        ChapterOptions = chapters.Select(chapter => new SelectListItem
        {
            Value = chapter.Id.ToString(),
            Text = $"Chương {chapter.Number}: {chapter.Title}"
        }).ToList();

        return true;
    }
}
