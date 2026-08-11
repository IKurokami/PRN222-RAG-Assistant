using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Data.Seed;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Domain.Enums;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Pages.Documents;

[Authorize(Policy = AppPolicies.ManageDocuments)]
public sealed class UploadModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IDocumentIndexingQueue _indexingQueue;
    private readonly IConfiguration _configuration;
    private readonly UserManager<ApplicationUser> _userManager;

    public UploadModel(
        ApplicationDbContext dbContext,
        IDocumentIndexingQueue indexingQueue,
        IConfiguration configuration,
        UserManager<ApplicationUser> userManager)
    {
        _dbContext = dbContext;
        _indexingQueue = indexingQueue;
        _configuration = configuration;
        _userManager = userManager;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public sealed class InputModel : IValidatableObject
    {
        private static readonly string[] AllowedExtensions = [".pdf", ".docx", ".pptx"];
        public const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB

        [Required(ErrorMessage = "Ti\u00eau \u0111\u1ec1 t\u00e0i li\u1ec7u l\u00e0 b\u1eaft bu\u1ed9c.")]
        [StringLength(200, ErrorMessage = "Ti\u00eau \u0111\u1ec1 kh\u00f4ng \u0111\u01b0\u1ee3c v\u01b0\u1ee3t qu\u00e1 200 k\u00fd t\u1ef1.")]
        [Display(Name = "Ti\u00eau \u0111\u1ec1 t\u00e0i li\u1ec7u")]
        public string Title { get; set; } = string.Empty;

        [Display(Name = "Ch\u01b0\u01a1ng (kh\u00f4ng b\u1eaft bu\u1ed9c)")]
        public Guid? ChapterId { get; set; }

        [Required(ErrorMessage = "Vui l\u00f2ng ch\u1ecdn file c\u1ea7n t\u1ea3i l\u00ean.")]
        [Display(Name = "File t\u00e0i li\u1ec7u (.pdf, .docx, .pptx)")]
        public IFormFile? File { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (File is null || File.Length == 0)
            {
                yield return new ValidationResult(
                    "File \u0111\u01b0\u1ee3c t\u1ea3i l\u00ean kh\u00f4ng h\u1ee3p l\u1ec7 ho\u1eb7c tr\u1ed1ng.",
                    [nameof(File)]);
                yield break;
            }

            if (File.Length > MaxFileSizeBytes)
            {
                yield return new ValidationResult(
                    $"K\u00edch th\u01b0\u1edbc file v\u01b0\u1ee3t qu\u00e1 gi\u1edbi h\u1ea1n t\u1ed1i \u0111a (50 MB).",
                    [nameof(File)]);
            }

            var extension = Path.GetExtension(File.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
            {
                yield return new ValidationResult(
                    $"\u0110\u1ecbnh d\u1ea1ng file '{extension}' kh\u00f4ng \u0111\u01b0\u1ee3c h\u1ed7 tr\u1ee3. C\u00e1c \u0111\u1ecbnh d\u1ea1ng \u0111\u01b0\u1ee3c ph\u00e9p: {string.Join(", ", AllowedExtensions)}",
                    [nameof(File)]);
            }
        }
    }

    public List<SelectListItem> ChapterOptions { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadChapterOptionsAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadChapterOptionsAsync(cancellationToken);
            return Page();
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        if (Input.File is null || Input.File.Length == 0)
        {
            ModelState.AddModelError("Input.File", "File tải lên không hợp lệ.");
            await LoadChapterOptionsAsync(cancellationToken);
            return Page();
        }

        var uploadsFolderSetting = _configuration["Rag:Storage:UploadsPath"] ?? "storage/uploads";
        var uploadsFolder = Path.IsPathRooted(uploadsFolderSetting)
            ? uploadsFolderSetting
            : Path.Combine(Directory.GetCurrentDirectory(), uploadsFolderSetting);

        Directory.CreateDirectory(uploadsFolder);

        var documentId = Guid.NewGuid();
        var extension = Path.GetExtension(Input.File.FileName).ToLowerInvariant();
        var storagePath = Path.Combine(uploadsFolder, $"{documentId}{extension}").Replace('\\', '/');

        await using (var stream = new FileStream(storagePath, FileMode.Create))
        {
            await Input.File.CopyToAsync(stream, cancellationToken);
        }

        var document = new Document
        {
            Id = documentId,
            SubjectId = SeedData.Prn222SubjectId,
            ChapterId = Input.ChapterId,
            UploadedByUserId = user.Id,
            Title = Input.Title.Trim(),
            OriginalFileName = Input.File.FileName,
            StoragePath = storagePath,
            ContentType = Input.File.ContentType ?? "application/octet-stream",
            FileExtension = extension,
            FileSizeBytes = Input.File.Length,
            IndexStatus = DocumentIndexStatus.Uploaded,
            UploadedAtUtc = DateTime.UtcNow
        };

        _dbContext.Documents.Add(document);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _indexingQueue.EnqueueAsync(document.Id, cancellationToken);

        TempData["StatusMessage"] = $"Đã tải thành công tài liệu '{document.Title}'. File đã được thêm vào hàng chờ index.";
        return RedirectToPage("./Index");
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
