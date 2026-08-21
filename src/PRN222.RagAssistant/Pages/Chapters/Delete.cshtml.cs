using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Models.Chapters;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Pages.Chapters;

[Authorize(Policy = AppPolicies.ManageDocuments)]
public class DeleteModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ISubjectAccessService _subjectAccessService;

    public DeleteModel(
        ApplicationDbContext dbContext,
        ISubjectAccessService subjectAccessService)
    {
        _dbContext = dbContext;
        _subjectAccessService = subjectAccessService;
    }

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public Guid SubjectId { get; set; }
    public string SubjectCode { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public int ChapterNumber { get; set; }
    public string ChapterTitle { get; set; } = string.Empty;
    public int AffectedDocumentCount { get; set; }

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

        var subject = await _dbContext.Subjects.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == chapter.SubjectId, cancellationToken);
        if (subject is null)
        {
            return NotFound();
        }

        var affectedDocumentCount = await _dbContext.Documents
            .AsNoTracking()
            .CountAsync(document => document.SubjectId == chapter.SubjectId && document.ChapterId == id, cancellationToken);

        Id = chapter.Id;
        SubjectId = subject.Id;
        SubjectCode = subject.Code;
        SubjectName = subject.Name;
        ChapterNumber = chapter.Number;
        ChapterTitle = chapter.Title;
        AffectedDocumentCount = affectedDocumentCount;

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

        var subjectId = chapter.SubjectId;
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var affectedDocuments = await _dbContext.Documents
            .Where(document => document.SubjectId == subjectId && document.ChapterId == Id)
            .ToListAsync(cancellationToken);

        foreach (var document in affectedDocuments)
        {
            document.ChapterId = null;
        }

        _dbContext.Chapters.Remove(chapter);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        TempData["StatusMessage"] = affectedDocuments.Count > 0
            ? $"Đã xóa chương {chapter.Number}: {chapter.Title}. {affectedDocuments.Count} tài liệu liên quan đã được bỏ gán chương (tài liệu vẫn còn trong hệ thống)."
            : $"Đã xóa chương {chapter.Number}: {chapter.Title} thành công.";

        return RedirectToPage("/Chapters/Index", new { subjectId });
    }
}
