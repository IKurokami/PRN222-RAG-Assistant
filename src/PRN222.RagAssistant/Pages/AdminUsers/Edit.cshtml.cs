using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Models.Admin;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Pages.AdminUsers;

[Authorize(Policy = AppPolicies.ManageUsers)]
public class EditModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;

    public EditModel(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public AdminUserRoleEditViewModel Input { get; set; } = new();

    public bool IsCurrentUser { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return NotFound();
        }

        var roles = await _userManager.GetRolesAsync(user);
        var managedRole = roles.FirstOrDefault(IsManagedRole) ?? AppRoles.Student;
        var currentUser = await _userManager.GetUserAsync(User);

        Id = id;
        Input = new AdminUserRoleEditViewModel
        {
            Id = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email ?? user.UserName ?? string.Empty,
            Role = managedRole
        };
        IsCurrentUser = currentUser?.Id == user.Id;
        Input.IsCurrentUser = IsCurrentUser;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Id != Input.Id)
        {
            return BadRequest();
        }

        var user = await _userManager.FindByIdAsync(Id.ToString());
        if (user is null)
        {
            return NotFound();
        }

        if (!IsManagedRole(Input.Role))
        {
            ModelState.AddModelError(nameof(Input.Role), "The selected role is not managed by this application.");
        }

        var currentUser = await _userManager.GetUserAsync(User);
        var currentRoles = await _userManager.GetRolesAsync(user);
        var isCurrentUser = currentUser?.Id == user.Id;

        if (isCurrentUser && !string.Equals(Input.Role, AppRoles.Admin, StringComparison.Ordinal))
        {
            ModelState.AddModelError(nameof(Input.Role), "You cannot remove your own Admin role.");
        }

        var isCurrentlyAdmin = currentRoles.Contains(AppRoles.Admin, StringComparer.Ordinal);
        if (isCurrentlyAdmin && !string.Equals(Input.Role, AppRoles.Admin, StringComparison.Ordinal))
        {
            var admins = await _userManager.GetUsersInRoleAsync(AppRoles.Admin);
            if (admins.Count <= 1)
            {
                ModelState.AddModelError(nameof(Input.Role), "The last Admin account cannot be demoted.");
            }
        }

        if (!ModelState.IsValid)
        {
            Input.DisplayName = user.DisplayName;
            Input.Email = user.Email ?? user.UserName ?? string.Empty;
            Input.IsCurrentUser = isCurrentUser;
            return Page();
        }

        if (!currentRoles.Contains(Input.Role, StringComparer.Ordinal))
        {
            var addResult = await _userManager.AddToRoleAsync(user, Input.Role);
            if (!addResult.Succeeded)
            {
                AddIdentityErrors(addResult);
                PopulateEditMetadata();
                return Page();
            }
        }

        var rolesToRemove = currentRoles
            .Where(role => IsManagedRole(role) && !string.Equals(role, Input.Role, StringComparison.Ordinal))
            .ToArray();

        if (rolesToRemove.Length > 0)
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
            if (!removeResult.Succeeded)
            {
                AddIdentityErrors(removeResult);
                PopulateEditMetadata();
                return Page();
            }
        }

        if (!string.Equals(Input.Role, AppRoles.SubjectLeader, StringComparison.Ordinal))
        {
            var claims = await _userManager.GetClaimsAsync(user);
            var subjectAssignments = claims
                .Where(claim => claim.Type == AppClaimTypes.ManagedSubject)
                .ToList();

            if (subjectAssignments.Count > 0)
            {
                var removeClaimsResult = await _userManager.RemoveClaimsAsync(user, subjectAssignments);
                if (!removeClaimsResult.Succeeded)
                {
                    AddIdentityErrors(removeClaimsResult);
                    PopulateEditMetadata();
                    return Page();
                }
            }
        }

        TempData["StatusMessage"] = $"Updated {user.DisplayName} to role {AppRoles.GetDisplayName(Input.Role)}.";
        return RedirectToPage("/AdminUsers/Index");
    }

    private void PopulateEditMetadata()
    {
        Input.IsCurrentUser = IsCurrentUser;
    }

    private static bool IsManagedRole(string roleName)
    {
        return AppRoles.All.Contains(roleName, StringComparer.Ordinal);
    }

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
    }
}
