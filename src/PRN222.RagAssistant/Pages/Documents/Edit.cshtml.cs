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
        [Required(ErrorMessage = "Ti\u00eau \u0111\u1ec1 t\u00e0i li\u1ec7u l\u00e0 b\u1eaft bu\u1ed9c.")]
        [StringLength(200, ErrorMessage = "Ti\u00eau \u0111\u1ec1 kh\u00f4ng \u0111\u01b0\u1ee3c v\u01b0\u1ee3t qu\u00e1 200 k\u00fd t\u1ef1.")]
        [Display(Name = "Ti\u00eau \u0111\u1ec1 t\u00e0i li\u1ec7u")]
        public string Title { get; set; } = string.Empty;

        [Display(Name = "Ch\u01b0\u01a1ng (kh\u00f4ng b\u1eaft bu\u1ed9c)")]
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
