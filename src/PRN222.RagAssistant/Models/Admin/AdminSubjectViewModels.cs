using System.ComponentModel.DataAnnotations;

namespace PRN222.RagAssistant.Models.Admin;

public sealed class AdminSubjectIndexViewModel
{
    public List<AdminSubjectListItemViewModel> Subjects { get; set; } = [];
    public string? StatusMessage { get; set; }
}

public sealed class AdminSubjectListItemViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int ChapterCount { get; set; }
    public int DocumentCount { get; set; }
    public List<string> SubjectLeaderNames { get; set; } = [];
}

public sealed class AdminSubjectFormViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [StringLength(32)]
    [Display(Name = "Subject code")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    [Display(Name = "Subject name")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}

public sealed class AdminSubjectLeadersViewModel
{
    public Guid SubjectId { get; set; }
    public string SubjectCode { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public List<Guid> SelectedLeaderIds { get; set; } = [];
    public List<AdminSubjectLeaderOptionViewModel> Leaders { get; set; } = [];
}

public sealed class AdminSubjectLeaderOptionViewModel
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
}
