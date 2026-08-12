using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Data.Seed;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Pages.Chapters;

[Authorize(Policy = AppPolicies.ManageDocuments)]
public sealed class CreateModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;

    public CreateModel(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

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

    public IActionResult OnGet() => Page();

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var duplicateExists = await _dbContext.Chapters
            .AnyAsync(c => c.SubjectId == SeedData.Prn222SubjectId && c.Number == Input.Number!.Value, cancellationToken);

        if (duplicateExists)
        {
            ModelState.AddModelError("Input.Number", $"Chương số {Input.Number} đã tồn tại trong môn PRN222.");
            return Page();
        }

        var chapter = new Chapter
        {
            Id = Guid.NewGuid(),
            SubjectId = SeedData.Prn222SubjectId,
            Number = Input.Number!.Value,
            Title = Input.Title.Trim()
        };

        _dbContext.Chapters.Add(chapter);
        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["StatusMessage"] = $"Đã tạo chương {chapter.Number}: {chapter.Title} thành công.";
        return RedirectToPage("./Index");
    }
}
