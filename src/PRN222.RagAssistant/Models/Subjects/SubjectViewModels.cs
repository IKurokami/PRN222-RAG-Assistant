namespace PRN222.RagAssistant.Models.Subjects;

public sealed class SubjectIndexViewModel
{
    public List<SubjectListItemViewModel> Subjects { get; set; } = [];
}

public sealed class SubjectListItemViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool CanManage { get; set; }
    public int ChapterCount { get; set; }
    public int DocumentCount { get; set; }
}
