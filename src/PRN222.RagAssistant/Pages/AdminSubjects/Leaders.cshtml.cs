using System.Security.Claims;
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
public class LeadersModel(
    ISubjectCatalogService subjectCatalogService,
    UserManager<ApplicationUser> userManager) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public Guid SubjectId { get; set; }
    public string SubjectCode { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;

    [BindProperty]
    public List<Guid> SelectedLeaderIds { get; set; } = new();

    public List<AdminSubjectLeaderOptionViewModel> Leaders { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var viewModel = await BuildLeaderAssignmentViewModelAsync(id, cancellationToken);
        if (viewModel is null)
        {
            return NotFound();
        }

        ApplyViewModel(viewModel);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var subject = await subjectCatalogService.GetSubjectAsync(
            Id,
            cancellationToken: cancellationToken);

        if (subject is null)
        {
            return NotFound();
        }

        SubjectId = Id;
        SubjectCode = subject.Code;
        SubjectName = subject.Name;

        var leaders = await userManager.GetUsersInRoleAsync(AppRoles.SubjectLeader);
        var validLeaderIds = leaders.Select(leader => leader.Id).ToHashSet();
        var selectedLeaderIds = SelectedLeaderIds.Distinct().ToHashSet();

        if (selectedLeaderIds.Any(selectedId => !validLeaderIds.Contains(selectedId)))
        {
            ModelState.AddModelError(
                nameof(SelectedLeaderIds),
                "Only users with the Subject Leader role can be assigned to a subject.");
        }

        if (!ModelState.IsValid)
        {
            var invalidViewModel = await BuildLeaderAssignmentViewModelAsync(Id, cancellationToken);
            if (invalidViewModel is null)
            {
                return NotFound();
            }

            SelectedLeaderIds = selectedLeaderIds.ToList();
            foreach (var option in invalidViewModel.Leaders)
            {
                option.IsSelected = selectedLeaderIds.Contains(option.UserId);
            }

            Leaders = invalidViewModel.Leaders;
            return Page();
        }

        var claimValue = Id.ToString("D");
        foreach (var leader in leaders)
        {
            var claims = await userManager.GetClaimsAsync(leader);
            var existingClaims = claims
                .Where(claim => claim.Type == AppClaimTypes.ManagedSubject && claim.Value == claimValue)
                .ToList();

            if (selectedLeaderIds.Contains(leader.Id))
            {
                if (existingClaims.Count == 0)
                {
                    var result = await userManager.AddClaimAsync(
                        leader,
                        new Claim(AppClaimTypes.ManagedSubject, claimValue));
                    if (!result.Succeeded)
                    {
                        AddIdentityErrors(result);
                    }
                }
            }
            else if (existingClaims.Count > 0)
            {
                var result = await userManager.RemoveClaimsAsync(leader, existingClaims);
                if (!result.Succeeded)
                {
                    AddIdentityErrors(result);
                }
            }
        }

        if (!ModelState.IsValid)
        {
            var failedViewModel = await BuildLeaderAssignmentViewModelAsync(Id, cancellationToken);
            if (failedViewModel is null)
            {
                return NotFound();
            }

            Leaders = failedViewModel.Leaders;
            return Page();
        }

        TempData["StatusMessage"] = $"Updated Subject Leader assignments for {subject.Code}.";
        return RedirectToPage("/AdminSubjects/Index");
    }

    private async Task<AdminSubjectLeadersViewModel?> BuildLeaderAssignmentViewModelAsync(
        Guid subjectId,
        CancellationToken cancellationToken)
    {
        var subject = await subjectCatalogService.GetSubjectAsync(
            subjectId,
            cancellationToken: cancellationToken);

        if (subject is null)
        {
            return null;
        }

        var leaders = await userManager.GetUsersInRoleAsync(AppRoles.SubjectLeader);
        var options = new List<AdminSubjectLeaderOptionViewModel>(leaders.Count);
        var selectedIds = new List<Guid>();
        var claimValue = subjectId.ToString("D");

        foreach (var leader in leaders.OrderBy(leader => leader.DisplayName).ThenBy(leader => leader.Email))
        {
            var claims = await userManager.GetClaimsAsync(leader);
            var selected = claims.Any(
                claim => claim.Type == AppClaimTypes.ManagedSubject && claim.Value == claimValue);

            if (selected)
            {
                selectedIds.Add(leader.Id);
            }

            options.Add(new AdminSubjectLeaderOptionViewModel
            {
                UserId = leader.Id,
                DisplayName = leader.DisplayName,
                Email = leader.Email ?? leader.UserName ?? string.Empty,
                IsSelected = selected
            });
        }

        return new AdminSubjectLeadersViewModel
        {
            SubjectId = subject.Id,
            SubjectCode = subject.Code,
            SubjectName = subject.Name,
            SelectedLeaderIds = selectedIds,
            Leaders = options
        };
    }

    private void ApplyViewModel(AdminSubjectLeadersViewModel viewModel)
    {
        Id = viewModel.SubjectId;
        SubjectId = viewModel.SubjectId;
        SubjectCode = viewModel.SubjectCode;
        SubjectName = viewModel.SubjectName;
        SelectedLeaderIds = viewModel.SelectedLeaderIds;
        Leaders = viewModel.Leaders;
    }

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
    }
}
