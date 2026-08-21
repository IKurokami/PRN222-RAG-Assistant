using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Models.Admin;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Pages.AdminSubjects;

[Authorize(Policy = AppPolicies.ManageSubjects)]
public class IndexModel(
    ISubjectCatalogService subjectCatalogService,
    UserManager<ApplicationUser> userManager) : PageModel
{
    public List<AdminSubjectListItemViewModel> Subjects { get; set; } = new();
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var subjects = await subjectCatalogService.GetSubjectsAsync(
            cancellationToken: cancellationToken);
        var subjectIds = subjects.Select(subject => subject.Id).ToArray();
        var chapterCounts = await subjectCatalogService.GetChapterCountsAsync(
            subjectIds,
            cancellationToken);
        var documentCounts = await subjectCatalogService.GetDocumentCountsAsync(
            subjectIds,
            cancellationToken);
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
        var leaders = await userManager.GetUsersInRoleAsync(AppRoles.SubjectLeader);

        foreach (var leader in leaders)
        {
            var claims = await userManager.GetClaimsAsync(leader);
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
