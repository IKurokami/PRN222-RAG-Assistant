using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Models.Admin;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Pages.AdminSubjects;

[Authorize(Policy = AppPolicies.ManageSubjects)]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;

    public CreateModel(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [BindProperty]
    public AdminSubjectFormViewModel Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        Normalize();

        if (await SubjectCodeExistsAsync(Input.Code, null, cancellationToken))
        {
            ModelState.AddModelError(nameof(Input.Code), "A subject with this code already exists.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var subject = new Subject
        {
            Id = Guid.NewGuid(),
            Code = Input.Code,
            Name = Input.Name,
            IsActive = Input.IsActive
        };

        _dbContext.Subjects.Add(subject);
        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["StatusMessage"] = $"Created subject {subject.Code} - {subject.Name}.";
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
