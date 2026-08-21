using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Models.Admin;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Pages.AdminSubjects;

[Authorize(Policy = AppPolicies.ManageSubjects)]
public class EditModel(ISubjectCatalogService subjectCatalogService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public AdminSubjectFormViewModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var subject = await subjectCatalogService.GetSubjectAsync(
            id,
            cancellationToken: cancellationToken);

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

        if (await subjectCatalogService.GetSubjectAsync(
                Id,
                cancellationToken: cancellationToken) is null)
        {
            return NotFound();
        }

        Normalize();

        if (await subjectCatalogService.SubjectCodeExistsAsync(
                Input.Code,
                Id,
                cancellationToken))
        {
            ModelState.AddModelError(nameof(Input.Code), "A subject with this code already exists.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var subject = await subjectCatalogService.UpdateSubjectAsync(
            Id,
            Input.Code,
            Input.Name,
            Input.IsActive,
            cancellationToken);

        if (subject is null)
        {
            return NotFound();
        }

        TempData["StatusMessage"] = $"Updated subject {subject.Code} - {subject.Name}.";
        return RedirectToPage("/AdminSubjects/Index");
    }

    private void Normalize()
    {
        Input.Code = (Input.Code ?? string.Empty).Trim().ToUpperInvariant();
        Input.Name = (Input.Name ?? string.Empty).Trim();
    }
}
