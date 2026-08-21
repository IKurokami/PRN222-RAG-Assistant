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
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ISubjectAccessService _subjectAccessService;

    public CreateModel(
        ApplicationDbContext dbContext,
        ISubjectAccessService subjectAccessService)
    {
        _dbContext = dbContext;
        _subjectAccessService = subjectAccessService;
    }

    [BindProperty(SupportsGet = true)]
    public Guid SubjectId { get; set; }

    public string SubjectCode { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;

    [BindProperty]
    public ChapterInputModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        if (!await _subjectAccessService.CanManageSubjectAsync(User, subjectId, cancellationToken))
        {
            return Forbid();
        }

        SubjectId = subjectId;

        var subject = await _dbContext.Subjects.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == subjectId, cancellationToken);
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
        if (!await _subjectAccessService.CanManageSubjectAsync(User, SubjectId, cancellationToken))
        {
            return Forbid();
        }

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
            chapter => chapter.SubjectId == SubjectId
                       && chapter.Number == Input.Number!.Value,
            cancellationToken);

        if (duplicateExists)
        {
            ModelState.AddModelError("Input.Number", $"Chương số {Input.Number} đã tồn tại trong môn {SubjectCode}.");
            return Page();
        }

        var chapter = new Chapter
        {
            Id = Guid.NewGuid(),
            SubjectId = SubjectId,
            Number = Input.Number!.Value,
            Title = Input.Title.Trim()
        };

        _dbContext.Chapters.Add(chapter);
        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["StatusMessage"] = $"Đã tạo chương {chapter.Number}: {chapter.Title} thành công.";
        return RedirectToPage("/Chapters/Index", new { subjectId = chapter.SubjectId });
    }
}
