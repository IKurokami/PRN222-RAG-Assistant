using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Models.Admin;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Controllers;

[Authorize(Policy = AppPolicies.ManageUsers)]
[Route("admin/users")]
public sealed class AdminUsersController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminUsersController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var users = await _userManager.Users
            .AsNoTracking()
            .OrderBy(user => user.DisplayName)
            .ThenBy(user => user.Email)
            .ToListAsync(cancellationToken);

        var items = new List<AdminUserListItemViewModel>(users.Count);

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            items.Add(new AdminUserListItemViewModel
            {
                Id = user.Id,
                DisplayName = user.DisplayName,
                Email = user.Email ?? user.UserName ?? string.Empty,
                CreatedAtUtc = user.CreatedAtUtc,
                Roles = roles.OrderBy(role => role).ToList(),
                IsCurrentUser = currentUser?.Id == user.Id
            });
        }

        return View(new AdminUserListViewModel
        {
            Users = items,
            StatusMessage = TempData["StatusMessage"] as string
        });
    }

    [HttpGet("create")]
    public IActionResult Create()
    {
        return View(new AdminUserCreateViewModel());
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminUserCreateViewModel viewModel)
    {
        if (!IsManagedRole(viewModel.Role))
        {
            ModelState.AddModelError(nameof(viewModel.Role), "The selected role is not managed by this application.");
        }

        if (!ModelState.IsValid)
        {
            return View(viewModel);
        }

        var email = viewModel.Email.Trim();
        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            ModelState.AddModelError(nameof(viewModel.Email), "An account with this email already exists.");
            return View(viewModel);
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = viewModel.DisplayName.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user, viewModel.Password);
        if (!createResult.Succeeded)
        {
            AddIdentityErrors(createResult);
            return View(viewModel);
        }

        var roleResult = await _userManager.AddToRoleAsync(user, viewModel.Role);
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            AddIdentityErrors(roleResult);
            return View(viewModel);
        }

        TempData["StatusMessage"] = $"Created {user.DisplayName} with role {AppRoles.GetDisplayName(viewModel.Role)}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:guid}/role")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return NotFound();
        }

        var roles = await _userManager.GetRolesAsync(user);
        var managedRole = roles.FirstOrDefault(IsManagedRole) ?? AppRoles.Student;
        var currentUser = await _userManager.GetUserAsync(User);

        return View(new AdminUserRoleEditViewModel
        {
            Id = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email ?? user.UserName ?? string.Empty,
            Role = managedRole,
            IsCurrentUser = currentUser?.Id == user.Id
        });
    }

    [HttpPost("{id:guid}/role")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, AdminUserRoleEditViewModel viewModel)
    {
        if (id != viewModel.Id)
        {
            return BadRequest();
        }

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return NotFound();
        }

        if (!IsManagedRole(viewModel.Role))
        {
            ModelState.AddModelError(nameof(viewModel.Role), "The selected role is not managed by this application.");
        }

        var currentUser = await _userManager.GetUserAsync(User);
        var currentRoles = await _userManager.GetRolesAsync(user);
        var isCurrentUser = currentUser?.Id == user.Id;

        if (isCurrentUser && !string.Equals(viewModel.Role, AppRoles.Admin, StringComparison.Ordinal))
        {
            ModelState.AddModelError(nameof(viewModel.Role), "You cannot remove your own Admin role.");
        }

        var isCurrentlyAdmin = currentRoles.Contains(AppRoles.Admin, StringComparer.Ordinal);
        if (isCurrentlyAdmin && !string.Equals(viewModel.Role, AppRoles.Admin, StringComparison.Ordinal))
        {
            var admins = await _userManager.GetUsersInRoleAsync(AppRoles.Admin);
            if (admins.Count <= 1)
            {
                ModelState.AddModelError(nameof(viewModel.Role), "The last Admin account cannot be demoted.");
            }
        }

        if (!ModelState.IsValid)
        {
            PopulateEditMetadata(viewModel, user, isCurrentUser);
            return View(viewModel);
        }

        if (!currentRoles.Contains(viewModel.Role, StringComparer.Ordinal))
        {
            var addResult = await _userManager.AddToRoleAsync(user, viewModel.Role);
            if (!addResult.Succeeded)
            {
                AddIdentityErrors(addResult);
                PopulateEditMetadata(viewModel, user, isCurrentUser);
                return View(viewModel);
            }
        }

        var rolesToRemove = currentRoles
            .Where(role => IsManagedRole(role) && !string.Equals(role, viewModel.Role, StringComparison.Ordinal))
            .ToArray();

        if (rolesToRemove.Length > 0)
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
            if (!removeResult.Succeeded)
            {
                AddIdentityErrors(removeResult);
                PopulateEditMetadata(viewModel, user, isCurrentUser);
                return View(viewModel);
            }
        }

        if (!string.Equals(viewModel.Role, AppRoles.SubjectLeader, StringComparison.Ordinal))
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
                    PopulateEditMetadata(viewModel, user, isCurrentUser);
                    return View(viewModel);
                }
            }
        }

        TempData["StatusMessage"] = $"Updated {user.DisplayName} to role {AppRoles.GetDisplayName(viewModel.Role)}.";
        return RedirectToAction(nameof(Index));
    }

    private static bool IsManagedRole(string roleName)
    {
        return AppRoles.All.Contains(roleName, StringComparer.Ordinal);
    }

    private static void PopulateEditMetadata(
        AdminUserRoleEditViewModel viewModel,
        ApplicationUser user,
        bool isCurrentUser)
    {
        viewModel.DisplayName = user.DisplayName;
        viewModel.Email = user.Email ?? user.UserName ?? string.Empty;
        viewModel.IsCurrentUser = isCurrentUser;
    }

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
    }
}
