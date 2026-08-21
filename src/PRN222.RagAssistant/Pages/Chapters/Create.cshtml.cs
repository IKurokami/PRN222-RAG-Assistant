using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Models.Chapters;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Pages.Chapters;

[Authorize(Policy = AppPolicies.ManageDocuments)]
public class CreateModel(
    ISubjectCatalogService subjectCatalogService,
    IChapterManagementService chapterManagementService,
    ISubjectAccessService subjectAccessService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid SubjectId { get; set; }

    public string SubjectCode { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;

    [BindProperty]
    public ChapterInputModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        if (!await subjectAccessService.CanManageSubjectAsync(User, subjectId, cancellationToken))
        {
            return Forbid();
        }

        SubjectId = subjectId;
        if (!await PopulateSubjectMetadataAsync(cancellationToken))
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!await subjectAccessService.CanManageSubjectAsync(User, SubjectId, cancellationToken))
        {
            return Forbid();
        }

        if (!await PopulateSubjectMetadataAsync(cancellationToken))
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var chapterNumber = Input.Number!.Value;
        if (await chapterManagementService.ChapterNumberExistsAsync(
                SubjectId,
                chapterNumber,
                cancellationToken: cancellationToken))
        {
            ModelState.AddModelError(
                "Input.Number",
                $"Chương số {Input.Number} đã tồn tại trong môn {SubjectCode}.");
            return Page();
        }

        var chapter = await chapterManagementService.CreateChapterAsync(
            SubjectId,
            chapterNumber,
            Input.Title.Trim(),
            cancellationToken);

        TempData["StatusMessage"] = $"Đã tạo chương {chapter.Number}: {chapter.Title} thành công.";
        return RedirectToPage("/Chapters/Index", new { subjectId = chapter.SubjectId });
    }

    private async Task<bool> PopulateSubjectMetadataAsync(CancellationToken cancellationToken)
    {
        var subject = await subjectCatalogService.GetSubjectAsync(
            SubjectId,
            cancellationToken: cancellationToken);

        if (subject is null)
        {
            return false;
        }

        SubjectCode = subject.Code;
        SubjectName = subject.Name;
        return true;
    }
}
