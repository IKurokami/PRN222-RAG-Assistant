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
    private readonly ILogger<UploadModel> _logger;

    public UploadModel(
        ApplicationDbContext dbContext,
        IDocumentIndexingQueue indexingQueue,
        IConfiguration configuration,
        UserManager<ApplicationUser> userManager,
        ILogger<UploadModel> logger)
    {
        _dbContext = dbContext;
        _indexingQueue = indexingQueue;
        _configuration = configuration;
        _userManager = userManager;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public sealed class InputModel : IValidatableObject
    {
        private static readonly string[] AllowedExtensions = [".pdf", ".docx", ".pptx"];
        public const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB

        [Required(ErrorMessage = "Tiêu đề tài liệu là bắt buộc.")]
        [StringLength(200, ErrorMessage = "Tiêu đề không được vượt quá 200 ký tự.")]
        [Display(Name = "Tiêu đề tài liệu")]
        public string Title { get; set; } = string.Empty;

        [Display(Name = "Chương (không bắt buộc)")]
        public Guid? ChapterId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn file cần tải lên.")]
        [Display(Name = "File tài liệu (.pdf, .docx, .pptx)")]
        public IFormFile? File { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (File is null || File.Length == 0)
            {
                yield return new ValidationResult(
                    "File được tải lên không hợp lệ hoặc trống.",
                    [nameof(File)]);
                yield break;
            }

            if (File.Length > MaxFileSizeBytes)
            {
                yield return new ValidationResult(
                    $"Kích thước file vượt quá giới hạn tối đa (50 MB).",
                    [nameof(File)]);
            }

            var extension = Path.GetExtension(File.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
            {
                yield return new ValidationResult(
                    $"Định dạng file '{extension}' không được hỗ trợ. Các định dạng được phép: {string.Join(", ", AllowedExtensions)}",
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

        // Blocker 4: Ghi file trước, cleanup nếu DB fail
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

        try
        {
            _dbContext.Documents.Add(document);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // DB failed — cleanup orphan file
            _logger.LogError(ex, "Failed to persist document record for {DocumentId}. Cleaning up uploaded file at {StoragePath}.", documentId, storagePath);

            try
            {
                if (System.IO.File.Exists(storagePath))
                {
                    System.IO.File.Delete(storagePath);
                }
            }
            catch (Exception cleanupEx)
            {
                _logger.LogWarning(cleanupEx, "Failed to clean up orphan file at {StoragePath}.", storagePath);
            }

            ModelState.AddModelError(string.Empty, "Lưu thông tin tài liệu thất bại. Vui lòng thử lại.");
            await LoadChapterOptionsAsync(cancellationToken);
            return Page();
        }

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
