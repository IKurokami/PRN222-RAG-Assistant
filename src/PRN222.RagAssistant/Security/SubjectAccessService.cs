using System.Security.Claims;
using PRN222.RagAssistant.Domain.Entities;

namespace PRN222.RagAssistant.Security;

public sealed class SubjectAccessService : ISubjectAccessService
{
    private readonly ISubjectAccessRepository _repository;

    public SubjectAccessService(ISubjectAccessRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<Subject>> GetAccessibleSubjectsAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var subjects = await _repository.GetSubjectsAsync(cancellationToken);

        if (user.IsInRole(AppRoles.Admin))
        {
            return subjects;
        }

        if (user.IsInRole(AppRoles.SubjectLeader))
        {
            var assignedSubjectIds = await GetAssignedSubjectIdsAsync(user, cancellationToken);
            return subjects
                .Where(subject => subject.IsActive || assignedSubjectIds.Contains(subject.Id))
                .ToList();
        }

        return subjects
            .Where(subject => subject.IsActive)
            .ToList();
    }

    public async Task<IReadOnlySet<Guid>> GetManageableSubjectIdsAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        if (user.IsInRole(AppRoles.Admin))
        {
            var subjects = await _repository.GetSubjectsAsync(cancellationToken);
            return subjects.Select(subject => subject.Id).ToHashSet();
        }

        if (!user.IsInRole(AppRoles.SubjectLeader))
        {
            return new HashSet<Guid>();
        }

        return await GetAssignedSubjectIdsAsync(user, cancellationToken);
    }

    public async Task<bool> CanViewSubjectAsync(
        ClaimsPrincipal user,
        Guid subjectId,
        CancellationToken cancellationToken = default)
    {
        if (subjectId == Guid.Empty)
        {
            return false;
        }

        var subject = await _repository.FindSubjectAsync(subjectId, cancellationToken);
        if (subject is null)
        {
            return false;
        }

        if (user.IsInRole(AppRoles.Admin))
        {
            return true;
        }

        if (subject.IsActive)
        {
            return true;
        }

        if (!user.IsInRole(AppRoles.SubjectLeader))
        {
            return false;
        }

        var assignedSubjectIds = await GetAssignedSubjectIdsAsync(user, cancellationToken);
        return assignedSubjectIds.Contains(subjectId);
    }

    public async Task<bool> CanManageSubjectAsync(
        ClaimsPrincipal user,
        Guid subjectId,
        CancellationToken cancellationToken = default)
    {
        if (subjectId == Guid.Empty)
        {
            return false;
        }

        if (await _repository.FindSubjectAsync(subjectId, cancellationToken) is null)
        {
            return false;
        }

        if (user.IsInRole(AppRoles.Admin))
        {
            return true;
        }

        if (!user.IsInRole(AppRoles.SubjectLeader))
        {
            return false;
        }

        var assignedSubjectIds = await GetAssignedSubjectIdsAsync(user, cancellationToken);
        return assignedSubjectIds.Contains(subjectId);
    }

    private async Task<IReadOnlySet<Guid>> GetAssignedSubjectIdsAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var userIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return new HashSet<Guid>();
        }

        return await _repository.GetAssignedSubjectIdsAsync(userId, cancellationToken);
    }
}
