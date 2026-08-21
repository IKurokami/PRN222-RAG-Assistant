using PRN222.RagAssistant.Domain.Entities;

namespace PRN222.RagAssistant.Application.Abstractions;

public interface ISubjectCatalogService
{
    Task<IReadOnlyList<Subject>> GetSubjectsAsync(
        bool activeOnly = false,
        CancellationToken cancellationToken = default);

    Task<Subject?> GetSubjectAsync(
        Guid subjectId,
        bool activeOnly = false,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, int>> GetChapterCountsAsync(
        IReadOnlyCollection<Guid> subjectIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, int>> GetDocumentCountsAsync(
        IReadOnlyCollection<Guid> subjectIds,
        CancellationToken cancellationToken = default);

    Task<bool> SubjectCodeExistsAsync(
        string code,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default);

    Task<Subject> CreateSubjectAsync(
        string code,
        string name,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task<Subject?> UpdateSubjectAsync(
        Guid subjectId,
        string code,
        string name,
        bool isActive,
        CancellationToken cancellationToken = default);
}
