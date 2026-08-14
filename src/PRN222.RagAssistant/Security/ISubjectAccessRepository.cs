using PRN222.RagAssistant.Domain.Entities;

namespace PRN222.RagAssistant.Security;

public interface ISubjectAccessRepository
{
    Task<IReadOnlyList<Subject>> GetSubjectsAsync(CancellationToken cancellationToken = default);

    Task<Subject?> FindSubjectAsync(Guid subjectId, CancellationToken cancellationToken = default);

    Task<IReadOnlySet<Guid>> GetAssignedSubjectIdsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
