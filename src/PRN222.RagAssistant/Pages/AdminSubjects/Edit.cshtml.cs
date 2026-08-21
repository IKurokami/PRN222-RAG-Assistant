using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Models.Admin;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Pages.AdminSubjects;

[Authorize(Policy = AppPolicies.ManageSubjects)]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;

    public EditModel(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public AdminSubjectFormViewModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var subject = await _dbContext.Subjects.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (subject is null)
        {
            return NotFound();
        }

        Id = id;
        Input = new AdminSubjectFormViewModel
        {
            Id = subject.Id,
            Code = subject.Code,
            Name = subject.Name,
            IsActive = subject.IsActive
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (Input.Id != Id)
        {
            return BadRequest();
        }

        var subject = await _dbContext.Subjects.FirstOrDefaultAsync(candidate => candidate.Id == Id, cancellationToken);
        if (subject is null)
        {
            return NotFound();
        }

        Normalize();

        if (await SubjectCodeExistsAsync(Input.Code, Id, cancellationToken))
        {
            ModelState.AddModelError(nameof(Input.Code), "A subject with this code already exists.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        subject.Code = Input.Code;
        subject.Name = Input.Name;
        subject.IsActive = Input.IsActive;
        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["StatusMessage"] = $"Updated subject {subject.Code} - {subject.Name}.";
        return RedirectToPage("/AdminSubjects/Index");
    }

    private void Normalize()
    {
        Input.Code = (Input.Code ?? string.Empty).Trim().ToUpperInvariant();
        Input.Name = (Input.Name ?? string.Empty).Trim();
    }

    private async Task<bool> SubjectCodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken)
    {
        return await _dbContext.Subjects.AsNoTracking().AnyAsync(
            subject => subject.Code == code && (!excludeId.HasValue || subject.Id != excludeId.Value),
            cancellationToken);
    }
}
