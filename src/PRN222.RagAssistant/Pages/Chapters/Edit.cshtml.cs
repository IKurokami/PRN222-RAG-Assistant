using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Models.Chapters;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Pages.Chapters;

[Authorize(Policy = AppPolicies.ManageDocuments)]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ISubjectAccessService _subjectAccessService;

    public EditModel(
        ApplicationDbContext dbContext,
        ISubjectAccessService subjectAccessService)
    {
        _dbContext = dbContext;
        _subjectAccessService = subjectAccessService;
    }

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
        var chapter = await _dbContext.Chapters
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (chapter is null)
        {
            return NotFound();
        }

        if (!await _subjectAccessService.CanManageSubjectAsync(User, chapter.SubjectId, cancellationToken))
        {
            return Forbid();
        }

        Id = chapter.Id;
        SubjectId = chapter.SubjectId;
        OriginalNumber = chapter.Number;
        OriginalTitle = chapter.Title;
        Input = new ChapterInputModel
        {
            Number = chapter.Number,
            Title = chapter.Title
        };

        var subject = await _dbContext.Subjects.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == SubjectId, cancellationToken);
        if (subject is null)
        {
            return NotFound();
        }

        SubjectCode = subject.Code;
        SubjectName = subject.Name;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var chapter = await _dbContext.Chapters.FirstOrDefaultAsync(candidate => candidate.Id == Id, cancellationToken);
        if (chapter is null)
        {
            return NotFound();
        }

        if (!await _subjectAccessService.CanManageSubjectAsync(User, chapter.SubjectId, cancellationToken))
        {
            return Forbid();
        }

        SubjectId = chapter.SubjectId;
        OriginalNumber = chapter.Number;
        OriginalTitle = chapter.Title;

        var subject = await _dbContext.Subjects.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == SubjectId, cancellationToken);
        if (subject is null)
        {
            return NotFound();
        }

        SubjectCode = subject.Code;
        SubjectName = subject.Name;

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var duplicateExists = await _dbContext.Chapters.AnyAsync(
            candidate => candidate.SubjectId == SubjectId
                         && candidate.Number == Input.Number!.Value
                         && candidate.Id != Id,
            cancellationToken);

        if (duplicateExists)
        {
            ModelState.AddModelError("Input.Number", $"Chương số {Input.Number} đã tồn tại trong môn {SubjectCode}.");
            return Page();
        }

        chapter.Number = Input.Number!.Value;
        chapter.Title = Input.Title.Trim();
        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["StatusMessage"] = $"Đã cập nhật chương {chapter.Number}: {chapter.Title} thành công.";
        return RedirectToPage("/Chapters/Index", new { subjectId = chapter.SubjectId });
    }
}
