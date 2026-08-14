using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;

namespace PRN222.RagAssistant.Security;

public sealed class SubjectAccessRepository : ISubjectAccessRepository
{
    private readonly ApplicationDbContext _dbContext;

    public SubjectAccessRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Subject>> GetSubjectsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Subjects
            .AsNoTracking()
            .OrderBy(subject => subject.Code)
            .ToListAsync(cancellationToken);
    }

    public Task<Subject?> FindSubjectAsync(
        Guid subjectId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Subjects
            .AsNoTracking()
            .FirstOrDefaultAsync(subject => subject.Id == subjectId, cancellationToken);
    }

    public async Task<IReadOnlySet<Guid>> GetAssignedSubjectIdsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
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
