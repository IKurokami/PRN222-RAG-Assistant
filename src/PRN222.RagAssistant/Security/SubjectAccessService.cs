using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;

namespace PRN222.RagAssistant.Security;

public sealed class SubjectAccessService : ISubjectAccessService
{
    private readonly ApplicationDbContext _dbContext;

    public SubjectAccessService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Subject>> GetAccessibleSubjectsAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        if (user.IsInRole(AppRoles.Admin))
        {
            return await _dbContext.Subjects
                .AsNoTracking()
                .OrderBy(subject => subject.Code)
                .ToListAsync(cancellationToken);
        }

        if (user.IsInRole(AppRoles.SubjectLeader))
        {
            var assignedSubjectIds = await GetAssignedSubjectIdsAsync(user, cancellationToken);

            return await _dbContext.Subjects
                .AsNoTracking()
                .Where(subject => subject.IsActive || assignedSubjectIds.Contains(subject.Id))
                .OrderBy(subject => subject.Code)
                .ToListAsync(cancellationToken);
        }

        return await _dbContext.Subjects
            .AsNoTracking()
            .Where(subject => subject.IsActive)
            .OrderBy(subject => subject.Code)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlySet<Guid>> GetManageableSubjectIdsAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        if (user.IsInRole(AppRoles.Admin))
        {
            return (await _dbContext.Subjects
                    .AsNoTracking()
                    .Select(subject => subject.Id)
                    .ToListAsync(cancellationToken))
                .ToHashSet();
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

        var subject = await _dbContext.Subjects
            .AsNoTracking()
            .Where(candidate => candidate.Id == subjectId)
            .Select(candidate => new { candidate.Id, candidate.IsActive })
            .FirstOrDefaultAsync(cancellationToken);

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

        if (!await _dbContext.Subjects.AsNoTracking().AnyAsync(subject => subject.Id == subjectId, cancellationToken))
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

    private async Task<HashSet<Guid>> GetAssignedSubjectIdsAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var userIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return [];
        }

        var claimValues = await _dbContext.UserClaims
            .AsNoTracking()
            .Where(claim => claim.UserId == userId && claim.ClaimType == AppClaimTypes.ManagedSubject)
            .Select(claim => claim.ClaimValue)
            .ToListAsync(cancellationToken);

        var subjectIds = new HashSet<Guid>();
        foreach (var claimValue in claimValues)
        {
            if (Guid.TryParse(claimValue, out var subjectId))
            {
                subjectIds.Add(subjectId);
            }
        }

        return subjectIds;
    }
}
