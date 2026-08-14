using System.Security.Claims;
using PRN222.RagAssistant.Domain.Entities;

namespace PRN222.RagAssistant.Security;

public interface ISubjectAccessService
{
    Task<IReadOnlyList<Subject>> GetAccessibleSubjectsAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<IReadOnlySet<Guid>> GetManageableSubjectIdsAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<bool> CanViewSubjectAsync(
        ClaimsPrincipal user,
        Guid subjectId,
        CancellationToken cancellationToken = default);

    Task<bool> CanManageSubjectAsync(
        ClaimsPrincipal user,
        Guid subjectId,
        CancellationToken cancellationToken = default);
}
