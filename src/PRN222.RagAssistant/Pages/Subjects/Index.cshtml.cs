using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Models.Subjects;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Pages.Subjects;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ISubjectAccessService _subjectAccessService;

    public IndexModel(
        ApplicationDbContext dbContext,
        ISubjectAccessService subjectAccessService)
    {
        _dbContext = dbContext;
        _subjectAccessService = subjectAccessService;
    }

    public List<SubjectListItemViewModel> Subjects { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var subjects = await _subjectAccessService.GetAccessibleSubjectsAsync(User, cancellationToken);
        var manageableSubjectIds = await _subjectAccessService.GetManageableSubjectIdsAsync(User, cancellationToken);
        var subjectIds = subjects.Select(subject => subject.Id).ToList();

        var chapterCounts = await _dbContext.Chapters
            .AsNoTracking()
            .Where(chapter => subjectIds.Contains(chapter.SubjectId))
            .GroupBy(chapter => chapter.SubjectId)
            .Select(group => new { SubjectId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.SubjectId, item => item.Count, cancellationToken);

        var documentCounts = await _dbContext.Documents
            .AsNoTracking()
            .Where(document => subjectIds.Contains(document.SubjectId))
            .GroupBy(document => document.SubjectId)
            .Select(group => new { SubjectId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.SubjectId, item => item.Count, cancellationToken);

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
