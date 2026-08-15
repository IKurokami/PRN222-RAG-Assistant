using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace PRN222.RagAssistant.Pages.Documents.Models;

public sealed class UploadInputModel : IValidatableObject
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
