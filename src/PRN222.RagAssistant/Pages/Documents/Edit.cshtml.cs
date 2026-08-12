using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Data.Seed;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Pages.Documents;

[Authorize(Policy = AppPolicies.ManageDocuments)]
public sealed class EditModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;

    public EditModel(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Tiêu đề tài liệu là bắt buộc.")]
        [StringLength(200, ErrorMessage = "Tiêu đề không được vượt quá 200 ký tự.")]
        [Display(Name = "Tiêu đề tài liệu")]
        public string Title { get; set; } = string.Empty;

        [Display(Name = "Chương (không bắt buộc)")]
        public Guid? ChapterId { get; set; }
    }

    public string DocumentTitle { get; set; } = string.Empty;
    public List<SelectListItem> ChapterOptions { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await _dbContext.Documents.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        DocumentTitle = document.Title;
        Input.Title = document.Title;
        Input.ChapterId = document.ChapterId;

        await LoadChapterOptionsAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadChapterOptionsAsync(cancellationToken);
            return Page();
        }

        // Blocker 3: Server-side validate ChapterId thuộc PRN222
        if (Input.ChapterId.HasValue)
        {
            var chapterValid = await _dbContext.Chapters
                .AnyAsync(c => c.Id == Input.ChapterId.Value && c.SubjectId == SeedData.Prn222SubjectId, cancellationToken);

            if (!chapterValid)
            {
                ModelState.AddModelError("Input.ChapterId", "Chương được chọn không hợp lệ hoặc không thuộc môn PRN222.");
                await LoadChapterOptionsAsync(cancellationToken);
                return Page();
            }
        }

        var document = await _dbContext.Documents.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        document.Title = Input.Title.Trim();
        document.ChapterId = Input.ChapterId;

        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["StatusMessage"] = $"Đã cập nhật thông tin tài liệu '{document.Title}' thành công.";
        return RedirectToPage("./Details", new { id });
    }

    private async Task LoadChapterOptionsAsync(CancellationToken cancellationToken)
    {
        var chapters = await _dbContext.Chapters
            .Where(c => c.SubjectId == SeedData.Prn222SubjectId)
            .OrderBy(c => c.Number)
            .ToListAsync(cancellationToken);

        ChapterOptions = chapters
            .Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = $"Chương {c.Number}: {c.Title}"
            })
            .ToList();
    }
}
