using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Models.Chapters;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Pages.Chapters;

[Authorize]
public class IndexModel(
    ISubjectCatalogService subjectCatalogService,
    IChapterManagementService chapterManagementService,
    ISubjectAccessService subjectAccessService) : PageModel
{
    public Guid SubjectId { get; set; }
    public string SubjectCode { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public bool CanManageDocuments { get; set; }
    public string? StatusMessage { get; set; }
    public List<ChapterItemViewModel> Chapters { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid subjectId, CancellationToken cancellationToken)
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
        var chapterIds = chapters.Select(chapter => chapter.Id).ToArray();
        var countMap = await chapterManagementService.GetDocumentCountsAsync(
            subjectId,
            chapterIds,
            cancellationToken);

        SubjectId = subject.Id;
        SubjectCode = subject.Code;
        SubjectName = subject.Name;
        CanManageDocuments = await subjectAccessService.CanManageSubjectAsync(
            User,
            subjectId,
            cancellationToken);
        StatusMessage = TempData["StatusMessage"] as string;
        Chapters = chapters.Select(chapter => new ChapterItemViewModel
        {
            Id = chapter.Id,
            Number = chapter.Number,
            Title = chapter.Title,
            DocumentCount = countMap.GetValueOrDefault(chapter.Id)
        }).ToList();

        return Page();
    }
}
