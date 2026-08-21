using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Enums;

namespace PRN222.RagAssistant.Infrastructure.Services;

public sealed class HomePageService(ApplicationDbContext dbContext) : IHomePageService
{
    public async Task<HomePageSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var totalChapters = await dbContext.Chapters.CountAsync(cancellationToken);
        var totalDocuments = await dbContext.Documents.CountAsync(cancellationToken);
        var indexedDocuments = await dbContext.Documents
            .CountAsync(document => document.IndexStatus == DocumentIndexStatus.Indexed, cancellationToken);
        var subject = await dbContext.Subjects
            .AsNoTracking()
            .OrderBy(candidate => candidate.Code)
            .FirstOrDefaultAsync(cancellationToken);

        return new HomePageSnapshot(
            totalChapters,
            totalDocuments,
            indexedDocuments,
            subject?.Code,
            subject?.Name);
    }
}
