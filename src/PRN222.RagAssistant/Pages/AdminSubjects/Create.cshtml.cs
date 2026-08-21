using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Models.Admin;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Pages.AdminSubjects;

[Authorize(Policy = AppPolicies.ManageSubjects)]
public class CreateModel(ISubjectCatalogService subjectCatalogService) : PageModel
{
    [BindProperty]
    public AdminSubjectFormViewModel Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        Normalize();

        if (await subjectCatalogService.SubjectCodeExistsAsync(
                Input.Code,
                cancellationToken: cancellationToken))
        {
            ModelState.AddModelError(nameof(Input.Code), "A subject with this code already exists.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var subject = await subjectCatalogService.CreateSubjectAsync(
            Input.Code,
            Input.Name,
            Input.IsActive,
            cancellationToken);

        TempData["StatusMessage"] = $"Created subject {subject.Code} - {subject.Name}.";
        return RedirectToPage("/AdminSubjects/Index");
    }

    private void Normalize()
    {
        Input.Code = (Input.Code ?? string.Empty).Trim().ToUpperInvariant();
        Input.Name = (Input.Name ?? string.Empty).Trim();
    }
}
