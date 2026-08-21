using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;

namespace PRN222.RagAssistant.Infrastructure.Services;

public sealed class SubjectCatalogService(ApplicationDbContext dbContext) : ISubjectCatalogService
{
    public async Task<IReadOnlyList<Subject>> GetSubjectsAsync(
        bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Subjects.AsNoTracking();

        if (activeOnly)
        {
            query = query.Where(subject => subject.IsActive);
        }

        return await query
            .OrderBy(subject => subject.Code)
            .ToListAsync(cancellationToken);
    }

    public async Task<Subject?> GetSubjectAsync(
        Guid subjectId,
        bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Subjects.AsNoTracking();

        if (activeOnly)
        {
            query = query.Where(subject => subject.IsActive);
        }

        return await query.FirstOrDefaultAsync(
            subject => subject.Id == subjectId,
            cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetChapterCountsAsync(
        IReadOnlyCollection<Guid> subjectIds,
        CancellationToken cancellationToken = default)
    {
        if (subjectIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var ids = subjectIds.ToArray();
        return await dbContext.Chapters
            .AsNoTracking()
            .Where(chapter => ids.Contains(chapter.SubjectId))
            .GroupBy(chapter => chapter.SubjectId)
            .Select(group => new { SubjectId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.SubjectId, item => item.Count, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetDocumentCountsAsync(
        IReadOnlyCollection<Guid> subjectIds,
        CancellationToken cancellationToken = default)
    {
        if (subjectIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var ids = subjectIds.ToArray();
        return await dbContext.Documents
            .AsNoTracking()
            .Where(document => ids.Contains(document.SubjectId))
            .GroupBy(document => document.SubjectId)
            .Select(group => new { SubjectId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.SubjectId, item => item.Count, cancellationToken);
    }

    public Task<bool> SubjectCodeExistsAsync(
        string code,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Subjects
            .AsNoTracking()
            .AnyAsync(
                subject => subject.Code == code
                    && (!excludeId.HasValue || subject.Id != excludeId.Value),
                cancellationToken);
    }

    public async Task<Subject> CreateSubjectAsync(
        string code,
        string name,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var subject = new Subject
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            IsActive = isActive
        };

        dbContext.Subjects.Add(subject);
        await dbContext.SaveChangesAsync(cancellationToken);
        return subject;
    }

    public async Task<Subject?> UpdateSubjectAsync(
        Guid subjectId,
        string code,
        string name,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var subject = await dbContext.Subjects
            .FirstOrDefaultAsync(candidate => candidate.Id == subjectId, cancellationToken);

        if (subject is null)
        {
            return null;
        }

        subject.Code = code;
        subject.Name = name;
        subject.IsActive = isActive;
        await dbContext.SaveChangesAsync(cancellationToken);
        return subject;
    }
}
