using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Data.Seed;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Pages.Chapters;

[Authorize]
public sealed class IndexModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAuthorizationService _authorizationService;

    public IndexModel(ApplicationDbContext dbContext, IAuthorizationService authorizationService)
    {
        _dbContext = dbContext;
        _authorizationService = authorizationService;
    }

    public List<ChapterItemViewModel> Chapters { get; set; } = [];
    public bool CanManageDocuments { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var authResult = await _authorizationService.AuthorizeAsync(User, AppPolicies.ManageDocuments);
        CanManageDocuments = authResult.Succeeded;

        var chapters = await _dbContext.Chapters
            .Where(c => c.SubjectId == SeedData.Prn222SubjectId)
            .OrderBy(c => c.Number)
            .ToListAsync(cancellationToken);

        var chapterIds = chapters.Select(c => c.Id).ToList();

        var docCounts = await _dbContext.Documents
            .Where(d => d.SubjectId == SeedData.Prn222SubjectId
                        && d.ChapterId.HasValue
                        && chapterIds.Contains(d.ChapterId!.Value))
            .GroupBy(d => d.ChapterId!.Value)
            .Select(g => new { ChapterId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var countMap = docCounts.ToDictionary(x => x.ChapterId, x => x.Count);

        Chapters = chapters.Select(c => new ChapterItemViewModel
        {
            Id = c.Id,
            Number = c.Number,
            Title = c.Title,
            DocumentCount = countMap.TryGetValue(c.Id, out var cnt) ? cnt : 0
        }).ToList();

        return Page();
    }

    public sealed class ChapterItemViewModel
    {
        public Guid Id { get; set; }
        public int Number { get; set; }
        public string Title { get; set; } = string.Empty;
        public int DocumentCount { get; set; }
    }
}
