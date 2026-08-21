using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Pages.Chapters;

[Authorize(Policy = AppPolicies.ManageDocuments)]
public class DeleteModel(
    ISubjectCatalogService subjectCatalogService,
    IChapterManagementService chapterManagementService,
    ISubjectAccessService subjectAccessService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public Guid SubjectId { get; set; }
    public string SubjectCode { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public int ChapterNumber { get; set; }
    public string ChapterTitle { get; set; } = string.Empty;
    public int AffectedDocumentCount { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var chapter = await chapterManagementService.GetChapterAsync(id, cancellationToken);
        if (chapter is null)
        {
            return NotFound();
        }

        if (!await subjectAccessService.CanManageSubjectAsync(User, chapter.SubjectId, cancellationToken))
        {
            return Forbid();
        }

        var subject = await subjectCatalogService.GetSubjectAsync(
            chapter.SubjectId,
            cancellationToken: cancellationToken);

        if (subject is null)
        {
            return NotFound();
        }

        Id = chapter.Id;
        SubjectId = subject.Id;
        SubjectCode = subject.Code;
        SubjectName = subject.Name;
        ChapterNumber = chapter.Number;
        ChapterTitle = chapter.Title;
        AffectedDocumentCount = await chapterManagementService.GetDocumentCountAsync(
            chapter.SubjectId,
            chapter.Id,
            cancellationToken);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var chapter = await chapterManagementService.GetChapterAsync(Id, cancellationToken);
        if (chapter is null)
        {
            return NotFound();
        }

        if (!await subjectAccessService.CanManageSubjectAsync(User, chapter.SubjectId, cancellationToken))
        {
            return Forbid();
        }

        var result = await chapterManagementService.DeleteChapterAsync(Id, cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        TempData["StatusMessage"] = result.AffectedDocumentCount > 0
            ? $"Đã xóa chương {result.Chapter.Number}: {result.Chapter.Title}. {result.AffectedDocumentCount} tài liệu liên quan đã được bỏ gán chương (tài liệu vẫn còn trong hệ thống)."
            : $"Đã xóa chương {result.Chapter.Number}: {result.Chapter.Title} thành công.";

        return RedirectToPage("/Chapters/Index", new { subjectId = result.Chapter.SubjectId });
    }
}
