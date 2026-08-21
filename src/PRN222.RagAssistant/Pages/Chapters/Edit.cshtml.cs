using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Models.Chapters;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Pages.Chapters;

[Authorize(Policy = AppPolicies.ManageDocuments)]
public class EditModel(
    ISubjectCatalogService subjectCatalogService,
    IChapterManagementService chapterManagementService,
    ISubjectAccessService subjectAccessService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public Guid SubjectId { get; set; }
    public int OriginalNumber { get; set; }
    public string OriginalTitle { get; set; } = string.Empty;
    public string SubjectCode { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;

    [BindProperty]
    public ChapterInputModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var chapter = await chapterManagementService.GetChapterAsync(id, cancellationToken);
        if (chapter is null)
        {
            return NotFound();
        }

        if (!await subjectAccessService.CanManageSubjectAsync(User, chapter.SubjectId, cancellationToken))
        {
            return Forbid();
        }

        ApplyChapter(chapter);
        Input = new ChapterInputModel
        {
            Number = chapter.Number,
            Title = chapter.Title
        };

        if (!await PopulateSubjectMetadataAsync(cancellationToken))
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var chapter = await chapterManagementService.GetChapterAsync(Id, cancellationToken);
        if (chapter is null)
        {
            return NotFound();
        }

        if (!await subjectAccessService.CanManageSubjectAsync(User, chapter.SubjectId, cancellationToken))
        {
            return Forbid();
        }

        ApplyChapter(chapter);
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
                Id,
                cancellationToken))
        {
            ModelState.AddModelError(
                "Input.Number",
                $"Chương số {Input.Number} đã tồn tại trong môn {SubjectCode}.");
            return Page();
        }

        var updated = await chapterManagementService.UpdateChapterAsync(
            Id,
            chapterNumber,
            Input.Title.Trim(),
            cancellationToken);

        if (updated is null)
        {
            return NotFound();
        }

        TempData["StatusMessage"] = $"Đã cập nhật chương {updated.Number}: {updated.Title} thành công.";
        return RedirectToPage("/Chapters/Index", new { subjectId = updated.SubjectId });
    }

    private void ApplyChapter(PRN222.RagAssistant.Domain.Entities.Chapter chapter)
    {
        Id = chapter.Id;
        SubjectId = chapter.SubjectId;
        OriginalNumber = chapter.Number;
        OriginalTitle = chapter.Title;
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
