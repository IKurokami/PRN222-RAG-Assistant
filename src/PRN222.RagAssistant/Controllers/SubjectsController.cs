using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Models.Subjects;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Controllers;

[Authorize]
[Route("subjects")]
public sealed class SubjectsController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ISubjectAccessService _subjectAccessService;

    public SubjectsController(
        ApplicationDbContext dbContext,
        ISubjectAccessService subjectAccessService)
    {
        _dbContext = dbContext;
        _subjectAccessService = subjectAccessService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
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

        return View(new SubjectIndexViewModel
        {
            Subjects = subjects.Select(subject => new SubjectListItemViewModel
            {
                Id = subject.Id,
                Code = subject.Code,
                Name = subject.Name,
                IsActive = subject.IsActive,
                CanManage = manageableSubjectIds.Contains(subject.Id),
                ChapterCount = chapterCounts.GetValueOrDefault(subject.Id),
                DocumentCount = documentCounts.GetValueOrDefault(subject.Id)
            }).ToList()
        });
    }
}
