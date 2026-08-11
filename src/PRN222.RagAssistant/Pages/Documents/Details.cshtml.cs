using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Domain.Enums;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Pages.Documents;

[Authorize]
public sealed class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuthorizationService _authorizationService;

    public DetailsModel(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IAuthorizationService authorizationService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _authorizationService = authorizationService;
    }

    public DocumentDetailViewModel Document { get; set; } = null!;
    public bool CanManageDocuments { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var authResult = await _authorizationService.AuthorizeAsync(User, AppPolicies.ManageDocuments);
        CanManageDocuments = authResult.Succeeded;

        var entity = await _dbContext.Documents.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var subject = await _dbContext.Subjects.FirstOrDefaultAsync(s => s.Id == entity.SubjectId, cancellationToken);
        var chapter = entity.ChapterId.HasValue
            ? await _dbContext.Chapters.FirstOrDefaultAsync(c => c.Id == entity.ChapterId.Value, cancellationToken)
            : null;
        var uploader = await _userManager.FindByIdAsync(entity.UploadedByUserId.ToString());

        var chunkCount = await _dbContext.DocumentChunks
            .CountAsync(c => c.DocumentId == id, cancellationToken);

        Document = new DocumentDetailViewModel
        {
            Id = entity.Id,
            Title = entity.Title,
            OriginalFileName = entity.OriginalFileName,
            StoragePath = entity.StoragePath,
            ContentType = entity.ContentType,
            FileExtension = entity.FileExtension,
            FileSizeBytes = entity.FileSizeBytes,
            SubjectCode = subject?.Code ?? "PRN222",
            SubjectName = subject?.Name ?? "C# & .NET Application Development",
            ChapterNumber = chapter?.Number,
            ChapterTitle = chapter?.Title,
            UploadedByEmail = uploader?.Email ?? "Hệ thống",
            IndexStatus = entity.IndexStatus,
            IndexError = entity.IndexError,
            UploadedAtUtc = entity.UploadedAtUtc,
            IndexedAtUtc = entity.IndexedAtUtc,
            ChunkCount = chunkCount
        };

        return Page();
    }

    public sealed class DocumentDetailViewModel
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public string StoragePath { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public string FileExtension { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string SubjectCode { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public int? ChapterNumber { get; set; }
        public string? ChapterTitle { get; set; }
        public string UploadedByEmail { get; set; } = string.Empty;
        public DocumentIndexStatus IndexStatus { get; set; }
        public string? IndexError { get; set; }
        public DateTime UploadedAtUtc { get; set; }
        public DateTime? IndexedAtUtc { get; set; }
        public int ChunkCount { get; set; }

        public string FormattedSize => FileSizeBytes switch
        {
            < 1024 => $"{FileSizeBytes} B",
            < 1024 * 1024 => $"{FileSizeBytes / 1024.0:F1} KB",
            _ => $"{FileSizeBytes / (1024.0 * 1024.0):F1} MB"
        };
    }
}
