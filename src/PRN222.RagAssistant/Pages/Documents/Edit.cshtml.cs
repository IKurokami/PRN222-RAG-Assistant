using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Models.Documents;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Pages.Documents;

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

    [BindProperty(SupportsGet = true)]
    public Guid SubjectId { get; set; }

    public string SubjectCode { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public string DocumentTitle { get; set; } = string.Empty;
    public List<SelectListItem> ChapterOptions { get; set; } = new();

    [BindProperty]
    public DocumentEditInputModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await _dbContext.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (document is null)
        {
            return NotFound();
        }

        if (!await _subjectAccessService.CanManageSubjectAsync(User, document.SubjectId, cancellationToken))
        {
            return Forbid();
        }

        Id = document.Id;
        SubjectId = document.SubjectId;
        DocumentTitle = document.Title;
        Input = new DocumentEditInputModel
        {
            Title = document.Title,
            ChapterId = document.ChapterId
        };

        if (!await PopulateMetadataAsync(cancellationToken))
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var document = await _dbContext.Documents.FirstOrDefaultAsync(candidate => candidate.Id == Id, cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        if (!await _subjectAccessService.CanManageSubjectAsync(User, document.SubjectId, cancellationToken))
        {
            return Forbid();
        }

        SubjectId = document.SubjectId;
        DocumentTitle = document.Title;

        if (!await PopulateMetadataAsync(cancellationToken))
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (Input.ChapterId.HasValue)
        {
            var chapterValid = await _dbContext.Chapters.AnyAsync(
                chapter => chapter.Id == Input.ChapterId.Value
                           && chapter.SubjectId == document.SubjectId,
                cancellationToken);

            if (!chapterValid)
            {
                ModelState.AddModelError("Input.ChapterId", "Chương được chọn không hợp lệ hoặc không thuộc môn học này.");
                return Page();
            }
        }

        document.Title = Input.Title.Trim();
        document.ChapterId = Input.ChapterId;
        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["StatusMessage"] = $"Đã cập nhật thông tin tài liệu '{document.Title}' thành công.";
        return RedirectToPage("/Documents/Details", new { id = Id });
    }

    private async Task<bool> PopulateMetadataAsync(CancellationToken cancellationToken)
    {
        var subject = await _dbContext.Subjects
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == SubjectId, cancellationToken);

        if (subject is null)
        {
            return false;
        }

        SubjectCode = subject.Code;
        SubjectName = subject.Name;

        var chapters = await _dbContext.Chapters
            .AsNoTracking()
            .Where(chapter => chapter.SubjectId == SubjectId)
            .OrderBy(chapter => chapter.Number)
            .ToListAsync(cancellationToken);

        ChapterOptions = chapters.Select(chapter => new SelectListItem
        {
            Value = chapter.Id.ToString(),
            Text = $"Chương {chapter.Number}: {chapter.Title}"
        }).ToList();

        return true;
    }
}
