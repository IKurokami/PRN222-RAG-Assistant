using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Models.Chapters;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Pages.Chapters;

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

        var chapterIds = chapters.Select(chapter => chapter.Id).ToList();
        var docCounts = await _dbContext.Documents
            .AsNoTracking()
            .Where(document => document.SubjectId == subjectId
                               && document.ChapterId.HasValue
                               && chapterIds.Contains(document.ChapterId.Value))
            .GroupBy(document => document.ChapterId!.Value)
            .Select(group => new { ChapterId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var countMap = docCounts.ToDictionary(item => item.ChapterId, item => item.Count);

        SubjectId = subject.Id;
        SubjectCode = subject.Code;
        SubjectName = subject.Name;
        CanManageDocuments = await _subjectAccessService.CanManageSubjectAsync(User, subjectId, cancellationToken);
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
