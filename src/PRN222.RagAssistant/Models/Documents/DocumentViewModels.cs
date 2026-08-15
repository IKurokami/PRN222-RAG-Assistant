using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using PRN222.RagAssistant.Domain.Enums;

namespace PRN222.RagAssistant.Models.Documents;

public sealed class DocumentIndexViewModel
{
    public Guid SubjectId { get; set; }
    public string SubjectCode { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public List<DocumentItemViewModel> Documents { get; set; } = [];
    public List<SelectListItem> ChapterOptions { get; set; } = [];
    public Guid? SelectedChapterId { get; set; }
    public string SearchTerm { get; set; } = string.Empty;
    public DocumentIndexStatus? SelectedStatus { get; set; }
    public int TotalDocumentCount { get; set; }
    public bool CanManageDocuments { get; set; }
    public string? StatusMessage { get; set; }
}

public sealed class DocumentItemViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public int? ChapterNumber { get; set; }
    public string? ChapterTitle { get; set; }
    public long FileSizeBytes { get; set; }
    public DocumentIndexStatus IndexStatus { get; set; }
    public string? IndexError { get; set; }
    public DateTime UploadedAtUtc { get; set; }
    public DateTime? IndexedAtUtc { get; set; }

    public string FormattedSize => FileSizeBytes switch
    {
        < 1024 => $"{FileSizeBytes} B",
        < 1024 * 1024 => $"{FileSizeBytes / 1024.0:F1} KB",
        _ => $"{FileSizeBytes / (1024.0 * 1024.0):F1} MB"
    };
}

public sealed class DocumentUploadViewModel
{
    public Guid SubjectId { get; set; }
    public string SubjectCode { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public DocumentUploadInputModel Input { get; set; } = new();
    public List<SelectListItem> ChapterOptions { get; set; } = [];
}

public sealed class DocumentUploadInputModel : IValidatableObject
{
    private static readonly string[] AllowedExtensions = [".pdf", ".docx", ".pptx"];
    public const long MaxFileSizeBytes = 50 * 1024 * 1024;

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
                "Kích thước file vượt quá giới hạn tối đa (50 MB).",
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

public sealed class DocumentEditViewModel
{
    public Guid Id { get; set; }
    public Guid SubjectId { get; set; }
    public string SubjectCode { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public string DocumentTitle { get; set; } = string.Empty;
    public DocumentEditInputModel Input { get; set; } = new();
    public List<SelectListItem> ChapterOptions { get; set; } = [];
}

public sealed class DocumentEditInputModel
{
    [Required(ErrorMessage = "Tiêu đề tài liệu là bắt buộc.")]
    [StringLength(200, ErrorMessage = "Tiêu đề không được vượt quá 200 ký tự.")]
    [Display(Name = "Tiêu đề tài liệu")]
    public string Title { get; set; } = string.Empty;

    [Display(Name = "Chương (không bắt buộc)")]
    public Guid? ChapterId { get; set; }
}

public sealed class DocumentDetailsViewModel
{
    public DocumentDetailViewModel Document { get; set; } = new();
    public bool CanManageDocuments { get; set; }
    public DocumentChunkPreviewPageViewModel ChunkPreview { get; set; } = new();
}

public sealed class DocumentDetailViewModel
{
    public Guid Id { get; set; }
    public Guid SubjectId { get; set; }
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

    public string FormattedSize => FileSizeBytes switch
    {
        < 1024 => $"{FileSizeBytes} B",
        < 1024 * 1024 => $"{FileSizeBytes / 1024.0:F1} KB",
        _ => $"{FileSizeBytes / (1024.0 * 1024.0):F1} MB"
    };
}

public sealed class DocumentChunkPreviewPageViewModel
{
    public List<DocumentChunkPreviewItemViewModel> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int EmbeddedCount { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; }

    public bool AllChunksEmbedded => TotalCount > 0 && EmbeddedCount == TotalCount;
    public bool HasPreviousPage => CurrentPage > 1;
    public bool HasNextPage => CurrentPage < TotalPages;
}

public sealed class DocumentChunkPreviewItemViewModel
{
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = string.Empty;
    public int? PageNumber { get; set; }
    public int? SlideNumber { get; set; }
    public bool HasEmbedding { get; set; }

    public int CharacterCount => Content.Length;
}
