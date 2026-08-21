using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Models.Admin;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Pages.AdminSubjects;

[Authorize(Policy = AppPolicies.ManageSubjects)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;

    public IndexModel(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
    }

    public List<AdminSubjectListItemViewModel> Subjects { get; set; } = new();
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var subjects = await _dbContext.Subjects
            .AsNoTracking()
            .OrderBy(subject => subject.Code)
            .ToListAsync(cancellationToken);

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

        var leaderNames = await BuildLeaderNameMapAsync();

        Subjects = subjects.Select(subject => new AdminSubjectListItemViewModel
        {
            Id = subject.Id,
            Code = subject.Code,
            Name = subject.Name,
            IsActive = subject.IsActive,
            ChapterCount = chapterCounts.GetValueOrDefault(subject.Id),
            DocumentCount = documentCounts.GetValueOrDefault(subject.Id),
            SubjectLeaderNames = leaderNames.GetValueOrDefault(subject.Id) ?? []
        }).ToList();

        StatusMessage = TempData["StatusMessage"] as string;

        return Page();
    }

    private async Task<Dictionary<Guid, List<string>>> BuildLeaderNameMapAsync()
    {
        var map = new Dictionary<Guid, List<string>>();
        var leaders = await _userManager.GetUsersInRoleAsync(AppRoles.SubjectLeader);

        foreach (var leader in leaders)
        {
            var claims = await _userManager.GetClaimsAsync(leader);
            foreach (var claim in claims.Where(claim => claim.Type == AppClaimTypes.ManagedSubject))
            {
                if (!Guid.TryParse(claim.Value, out var subjectId))
                {
                    continue;
                }

                if (!map.TryGetValue(subjectId, out var names))
                {
                    names = [];
                    map[subjectId] = names;
                }

                names.Add(leader.DisplayName);
            }
        }

        foreach (var names in map.Values)
        {
            names.Sort(StringComparer.OrdinalIgnoreCase);
        }

        return map;
    }
}
