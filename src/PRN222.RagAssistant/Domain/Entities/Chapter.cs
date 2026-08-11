namespace PRN222.RagAssistant.Domain.Entities;

public sealed class Chapter
{
    public Guid Id { get; set; }

    public Guid SubjectId { get; set; }

    public int Number { get; set; }

    public string Title { get; set; } = string.Empty;
}
