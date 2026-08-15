using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace PRN222.RagAssistant.Pages.Documents.Models;

public sealed class EditInputModel
{
    [Required(ErrorMessage = "Tiêu đề tài liệu là bắt buộc.")]
    [StringLength(200, ErrorMessage = "Tiêu đề không được vượt quá 200 ký tự.")]
    [Display(Name = "Tiêu đề tài liệu")]
    public string Title { get; set; } = string.Empty;

    [Display(Name = "Chương (không bắt buộc)")]
    public Guid? ChapterId { get; set; }
}
