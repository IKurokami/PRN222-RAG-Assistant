using System.ComponentModel.DataAnnotations;

namespace PRN222.RagAssistant.Models.Chapters;

public sealed class ChapterIndexViewModel
{
    public List<ChapterItemViewModel> Chapters { get; set; } = [];
    public bool CanManageDocuments { get; set; }
    public string? StatusMessage { get; set; }
}

public sealed class ChapterItemViewModel
{
    public Guid Id { get; set; }
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public int DocumentCount { get; set; }
}

public sealed class ChapterCreateViewModel
{
    public ChapterInputModel Input { get; set; } = new();
}

public sealed class ChapterEditViewModel
{
    public Guid Id { get; set; }
    public int OriginalNumber { get; set; }
    public string OriginalTitle { get; set; } = string.Empty;
    public ChapterInputModel Input { get; set; } = new();
}

public sealed class ChapterInputModel
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

public sealed class ChapterDeleteViewModel
{
    public Guid Id { get; set; }
    public int ChapterNumber { get; set; }
    public string ChapterTitle { get; set; } = string.Empty;
    public int AffectedDocumentCount { get; set; }
}
