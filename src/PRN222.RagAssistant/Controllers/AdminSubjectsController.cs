using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Models.Admin;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Controllers;

[Authorize(Policy = AppPolicies.ManageSubjects)]
[Route("admin/subjects")]
public sealed class AdminSubjectsController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminSubjectsController(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
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

        return View(new AdminSubjectIndexViewModel
        {
            StatusMessage = TempData["StatusMessage"] as string,
            Subjects = subjects.Select(subject => new AdminSubjectListItemViewModel
            {
                Id = subject.Id,
                Code = subject.Code,
                Name = subject.Name,
                IsActive = subject.IsActive,
                ChapterCount = chapterCounts.GetValueOrDefault(subject.Id),
                DocumentCount = documentCounts.GetValueOrDefault(subject.Id),
                SubjectLeaderNames = leaderNames.GetValueOrDefault(subject.Id) ?? []
            }).ToList()
        });
    }

    [HttpGet("create")]
    public IActionResult Create() => View(new AdminSubjectFormViewModel());

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        AdminSubjectFormViewModel viewModel,
        CancellationToken cancellationToken)
    {
        Normalize(viewModel);

        if (await SubjectCodeExistsAsync(viewModel.Code, null, cancellationToken))
        {
            ModelState.AddModelError(nameof(viewModel.Code), "A subject with this code already exists.");
        }

        if (!ModelState.IsValid)
        {
            return View(viewModel);
        }

        var subject = new Subject
        {
            Id = Guid.NewGuid(),
            Code = viewModel.Code,
            Name = viewModel.Name,
            IsActive = viewModel.IsActive
        };

        _dbContext.Subjects.Add(subject);
        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["StatusMessage"] = $"Created subject {subject.Code} - {subject.Name}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var subject = await _dbContext.Subjects.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (subject is null)
        {
            return NotFound();
        }

        return View(new AdminSubjectFormViewModel
        {
            Id = subject.Id,
            Code = subject.Code,
            Name = subject.Name,
            IsActive = subject.IsActive
        });
    }

    [HttpPost("{id:guid}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        Guid id,
        AdminSubjectFormViewModel viewModel,
        CancellationToken cancellationToken)
    {
        if (viewModel.Id != id)
        {
            return BadRequest();
        }

        var subject = await _dbContext.Subjects.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (subject is null)
        {
            return NotFound();
        }

        Normalize(viewModel);

        if (await SubjectCodeExistsAsync(viewModel.Code, id, cancellationToken))
        {
            ModelState.AddModelError(nameof(viewModel.Code), "A subject with this code already exists.");
        }

        if (!ModelState.IsValid)
        {
            return View(viewModel);
        }

        subject.Code = viewModel.Code;
        subject.Name = viewModel.Name;
        subject.IsActive = viewModel.IsActive;
        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["StatusMessage"] = $"Updated subject {subject.Code} - {subject.Name}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:guid}/leaders")]
    public async Task<IActionResult> Leaders(Guid id, CancellationToken cancellationToken)
    {
        var viewModel = await BuildLeaderAssignmentViewModelAsync(id, cancellationToken);
        return viewModel is null ? NotFound() : View(viewModel);
    }

    [HttpPost("{id:guid}/leaders")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Leaders(
        Guid id,
        AdminSubjectLeadersViewModel viewModel,
        CancellationToken cancellationToken)
    {
        if (viewModel.SubjectId != id)
        {
            return BadRequest();
        }

        var subject = await _dbContext.Subjects.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (subject is null)
        {
            return NotFound();
        }

        var leaders = await _userManager.GetUsersInRoleAsync(AppRoles.SubjectLeader);
        var validLeaderIds = leaders.Select(leader => leader.Id).ToHashSet();
        var selectedLeaderIds = viewModel.SelectedLeaderIds.Distinct().ToHashSet();

        if (selectedLeaderIds.Any(selectedId => !validLeaderIds.Contains(selectedId)))
        {
            ModelState.AddModelError(nameof(viewModel.SelectedLeaderIds), "Only users with the Subject Leader role can be assigned to a subject.");
        }

        if (!ModelState.IsValid)
        {
            var invalidViewModel = await BuildLeaderAssignmentViewModelAsync(id, cancellationToken);
            if (invalidViewModel is null)
            {
                return NotFound();
            }

            invalidViewModel.SelectedLeaderIds = selectedLeaderIds.ToList();
            foreach (var option in invalidViewModel.Leaders)
            {
                option.IsSelected = selectedLeaderIds.Contains(option.UserId);
            }

            return View(invalidViewModel);
        }

        var claimValue = id.ToString("D");
        foreach (var leader in leaders)
        {
            var claims = await _userManager.GetClaimsAsync(leader);
            var existingClaims = claims
                .Where(claim => claim.Type == AppClaimTypes.ManagedSubject && claim.Value == claimValue)
                .ToList();

            if (selectedLeaderIds.Contains(leader.Id))
            {
                if (existingClaims.Count == 0)
                {
                    var result = await _userManager.AddClaimAsync(leader, new Claim(AppClaimTypes.ManagedSubject, claimValue));
                    if (!result.Succeeded)
                    {
                        AddIdentityErrors(result);
                    }
                }
            }
            else if (existingClaims.Count > 0)
            {
                var result = await _userManager.RemoveClaimsAsync(leader, existingClaims);
                if (!result.Succeeded)
                {
                    AddIdentityErrors(result);
                }
            }
        }

        if (!ModelState.IsValid)
        {
            var failedViewModel = await BuildLeaderAssignmentViewModelAsync(id, cancellationToken);
            return failedViewModel is null ? NotFound() : View(failedViewModel);
        }

        TempData["StatusMessage"] = $"Updated Subject Leader assignments for {subject.Code}.";
        return RedirectToAction(nameof(Index));
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

    private async Task<AdminSubjectLeadersViewModel?> BuildLeaderAssignmentViewModelAsync(
        Guid subjectId,
        CancellationToken cancellationToken)
    {
        var subject = await _dbContext.Subjects.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == subjectId, cancellationToken);
        if (subject is null)
        {
            return null;
        }

        var leaders = await _userManager.GetUsersInRoleAsync(AppRoles.SubjectLeader);
        var options = new List<AdminSubjectLeaderOptionViewModel>(leaders.Count);
        var selectedIds = new List<Guid>();
        var claimValue = subjectId.ToString("D");

        foreach (var leader in leaders.OrderBy(leader => leader.DisplayName).ThenBy(leader => leader.Email))
        {
            var claims = await _userManager.GetClaimsAsync(leader);
            var selected = claims.Any(claim => claim.Type == AppClaimTypes.ManagedSubject && claim.Value == claimValue);
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

    private async Task<bool> SubjectCodeExistsAsync(
        string code,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Subjects.AsNoTracking().AnyAsync(
            subject => subject.Code == code && (!excludeId.HasValue || subject.Id != excludeId.Value),
            cancellationToken);
    }

    private static void Normalize(AdminSubjectFormViewModel viewModel)
    {
        viewModel.Code = (viewModel.Code ?? string.Empty).Trim().ToUpperInvariant();
        viewModel.Name = (viewModel.Name ?? string.Empty).Trim();
    }

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
    }
}
