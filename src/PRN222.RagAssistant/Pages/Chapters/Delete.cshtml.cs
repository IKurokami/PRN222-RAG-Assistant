using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Data.Seed;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Pages.Chapters;

[Authorize(Policy = AppPolicies.ManageDocuments)]
public sealed class DeleteModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;

    public DeleteModel(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public int ChapterNumber { get; set; }
    public string ChapterTitle { get; set; } = string.Empty;
    public int AffectedDocumentCount { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var chapter = await _dbContext.Chapters
            .FirstOrDefaultAsync(c => c.Id == id && c.SubjectId == SeedData.Prn222SubjectId, cancellationToken);

        if (chapter is null)
        {
            return NotFound();
        }

        ChapterNumber = chapter.Number;
        ChapterTitle = chapter.Title;

        AffectedDocumentCount = await _dbContext.Documents
            .CountAsync(d => d.ChapterId == id, cancellationToken);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken cancellationToken)
    {
        var chapter = await _dbContext.Chapters
            .FirstOrDefaultAsync(c => c.Id == id && c.SubjectId == SeedData.Prn222SubjectId, cancellationToken);

        if (chapter is null)
        {
            return NotFound();
        }

        // Trong một transaction:
        // 1. Set ChapterId = null cho mọi Document đang tham chiếu chapter này
        // 2. Xóa chapter
        // Không xóa Document.
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var affectedDocuments = await _dbContext.Documents
            .Where(d => d.ChapterId == id)
            .ToListAsync(cancellationToken);

        foreach (var doc in affectedDocuments)
        {
            doc.ChapterId = null;
        }

        _dbContext.Chapters.Remove(chapter);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var message = affectedDocuments.Count > 0
            ? $"Đã xóa chương {chapter.Number}: {chapter.Title}. {affectedDocuments.Count} tài liệu liên quan đã được bỏ gán chương (tài liệu vẫn còn trong hệ thống)."
            : $"Đã xóa chương {chapter.Number}: {chapter.Title} thành công.";

        TempData["StatusMessage"] = message;
        return RedirectToPage("./Index");
    }
}
