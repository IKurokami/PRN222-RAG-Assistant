using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;

namespace PRN222.RagAssistant.Infrastructure.Services;

public sealed class ChapterManagementService(ApplicationDbContext dbContext) : IChapterManagementService
{
    public async Task<IReadOnlyList<Chapter>> GetChaptersAsync(
        Guid subjectId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Chapters
            .AsNoTracking()
            .Where(chapter => chapter.SubjectId == subjectId)
            .OrderBy(chapter => chapter.Number)
            .ToListAsync(cancellationToken);
    }

    public Task<Chapter?> GetChapterAsync(
        Guid chapterId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Chapters
            .AsNoTracking()
            .FirstOrDefaultAsync(chapter => chapter.Id == chapterId, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetDocumentCountsAsync(
        Guid subjectId,
        IReadOnlyCollection<Guid> chapterIds,
        CancellationToken cancellationToken = default)
    {
        if (chapterIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var ids = chapterIds.ToArray();
        return await dbContext.Documents
            .AsNoTracking()
            .Where(document => document.SubjectId == subjectId
                && document.ChapterId.HasValue
                && ids.Contains(document.ChapterId.Value))
            .GroupBy(document => document.ChapterId!.Value)
            .Select(group => new { ChapterId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.ChapterId, item => item.Count, cancellationToken);
    }

    public Task<int> GetDocumentCountAsync(
        Guid subjectId,
        Guid chapterId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Documents
            .AsNoTracking()
            .CountAsync(
                document => document.SubjectId == subjectId && document.ChapterId == chapterId,
                cancellationToken);
    }

    public Task<bool> ChapterNumberExistsAsync(
        Guid subjectId,
        int chapterNumber,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Chapters.AnyAsync(
            chapter => chapter.SubjectId == subjectId
                && chapter.Number == chapterNumber
                && (!excludeId.HasValue || chapter.Id != excludeId.Value),
            cancellationToken);
    }

    public async Task<Chapter> CreateChapterAsync(
        Guid subjectId,
        int chapterNumber,
        string title,
        CancellationToken cancellationToken = default)
    {
        var chapter = new Chapter
        {
            Id = Guid.NewGuid(),
            SubjectId = subjectId,
            Number = chapterNumber,
            Title = title
        };

        dbContext.Chapters.Add(chapter);
        await dbContext.SaveChangesAsync(cancellationToken);
        return chapter;
    }

    public async Task<Chapter?> UpdateChapterAsync(
        Guid chapterId,
        int chapterNumber,
        string title,
        CancellationToken cancellationToken = default)
    {
        var chapter = await dbContext.Chapters
            .FirstOrDefaultAsync(candidate => candidate.Id == chapterId, cancellationToken);

        if (chapter is null)
        {
            return null;
        }

        chapter.Number = chapterNumber;
        chapter.Title = title;
        await dbContext.SaveChangesAsync(cancellationToken);
        return chapter;
    }

    public async Task<ChapterDeleteResult?> DeleteChapterAsync(
        Guid chapterId,
        CancellationToken cancellationToken = default)
    {
        var chapter = await dbContext.Chapters
            .FirstOrDefaultAsync(candidate => candidate.Id == chapterId, cancellationToken);

        if (chapter is null)
        {
            return null;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var affectedDocuments = await dbContext.Documents
            .Where(document => document.SubjectId == chapter.SubjectId && document.ChapterId == chapterId)
            .ToListAsync(cancellationToken);

        foreach (var document in affectedDocuments)
        {
            document.ChapterId = null;
        }

        dbContext.Chapters.Remove(chapter);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ChapterDeleteResult(chapter, affectedDocuments.Count);
    }
}
