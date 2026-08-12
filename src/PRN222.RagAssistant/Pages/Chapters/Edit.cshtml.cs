using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Data.Seed;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Pages.Chapters;

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

    public int OriginalNumber { get; set; }
    public string OriginalTitle { get; set; } = string.Empty;

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Số thứ tự chương là bắt buộc.")]
        [Range(1, 999, ErrorMessage = "Số thứ tự phải từ 1 đến 999.")]
        [Display(Name = "Số thứ tự chương")]
        public int? Number { get; set; }

        [Required(ErrorMessage = "Tên chương là bắt buộc.")]
        [StringLength(300, ErrorMessage = "Tên chương không được vượt quá 300 ký tự.")]
        [Display(Name = "Tên chương")]
        public string Title { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var chapter = await _dbContext.Chapters
            .FirstOrDefaultAsync(c => c.Id == id && c.SubjectId == SeedData.Prn222SubjectId, cancellationToken);

        if (chapter is null)
        {
            return NotFound();
        }

        OriginalNumber = chapter.Number;
        OriginalTitle = chapter.Title;
        Input.Number = chapter.Number;
        Input.Title = chapter.Title;

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

        OriginalNumber = chapter.Number;
        OriginalTitle = chapter.Title;

        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Validate unique (SubjectId, Number) — exclude self
        var duplicateExists = await _dbContext.Chapters
            .AnyAsync(c => c.SubjectId == SeedData.Prn222SubjectId
                           && c.Number == Input.Number!.Value
                           && c.Id != id, cancellationToken);

        if (duplicateExists)
        {
            ModelState.AddModelError("Input.Number", $"Chương số {Input.Number} đã tồn tại trong môn PRN222.");
            return Page();
        }

        chapter.Number = Input.Number!.Value;
        chapter.Title = Input.Title.Trim();

        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["StatusMessage"] = $"Đã cập nhật chương {chapter.Number}: {chapter.Title} thành công.";
        return RedirectToPage("./Index");
    }
}
