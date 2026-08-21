namespace PRN222.RagAssistant.Application.Abstractions;

public interface IHomePageService
{
    Task<HomePageSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}

public sealed record HomePageSnapshot(
    int TotalChapters,
    int TotalDocuments,
    int IndexedDocuments,
    string? SubjectCode,
    string? SubjectName);
