using PRN222.RagAssistant.Domain.Entities;

namespace PRN222.RagAssistant.Application.Abstractions;

public interface IChapterManagementService
{
    Task<IReadOnlyList<Chapter>> GetChaptersAsync(
        Guid subjectId,
        CancellationToken cancellationToken = default);

    Task<Chapter?> GetChapterAsync(
        Guid chapterId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, int>> GetDocumentCountsAsync(
        Guid subjectId,
        IReadOnlyCollection<Guid> chapterIds,
        CancellationToken cancellationToken = default);

    Task<int> GetDocumentCountAsync(
        Guid subjectId,
        Guid chapterId,
        CancellationToken cancellationToken = default);

    Task<bool> ChapterNumberExistsAsync(
        Guid subjectId,
        int chapterNumber,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default);

    Task<Chapter> CreateChapterAsync(
        Guid subjectId,
        int chapterNumber,
        string title,
        CancellationToken cancellationToken = default);

    Task<Chapter?> UpdateChapterAsync(
        Guid chapterId,
        int chapterNumber,
        string title,
        CancellationToken cancellationToken = default);

    Task<ChapterDeleteResult?> DeleteChapterAsync(
        Guid chapterId,
        CancellationToken cancellationToken = default);
}

public sealed record ChapterDeleteResult(
    Chapter Chapter,
    int AffectedDocumentCount);
