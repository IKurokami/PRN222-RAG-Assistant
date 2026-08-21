using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Models.Subjects;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Pages.Subjects;

[Authorize]
public class IndexModel(
    ISubjectCatalogService subjectCatalogService,
    ISubjectAccessService subjectAccessService) : PageModel
{
    public List<SubjectListItemViewModel> Subjects { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var subjects = await subjectAccessService.GetAccessibleSubjectsAsync(User, cancellationToken);
        var manageableSubjectIds = await subjectAccessService.GetManageableSubjectIdsAsync(User, cancellationToken);
        var subjectIds = subjects.Select(subject => subject.Id).ToArray();

        var chapterCounts = await subjectCatalogService.GetChapterCountsAsync(
            subjectIds,
            cancellationToken);
        var documentCounts = await subjectCatalogService.GetDocumentCountsAsync(
            subjectIds,
            cancellationToken);

        Subjects = subjects.Select(subject => new SubjectListItemViewModel
        {
            Id = subject.Id,
            Code = subject.Code,
            Name = subject.Name,
            IsActive = subject.IsActive,
            CanManage = manageableSubjectIds.Contains(subject.Id),
            ChapterCount = chapterCounts.GetValueOrDefault(subject.Id),
            DocumentCount = documentCounts.GetValueOrDefault(subject.Id)
        }).ToList();

        return Page();
    }
}
