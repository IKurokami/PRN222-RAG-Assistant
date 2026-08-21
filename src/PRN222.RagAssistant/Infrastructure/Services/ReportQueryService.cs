using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Application.Models;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Enums;

namespace PRN222.RagAssistant.Infrastructure.Services;

public sealed class ReportQueryService : IReportQueryService
{
    private readonly ApplicationDbContext _dbContext;

    public ReportQueryService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SubjectReportSnapshot?> GetSubjectReportAsync(
        Guid subjectId,
        CancellationToken cancellationToken = default)
    {
        var subject = await _dbContext.Subjects
            .AsNoTracking()
            .Where(candidate => candidate.Id == subjectId)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.Code,
                candidate.Name
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (subject is null)
        {
            return null;
        }

        var documentsQuery = _dbContext.Documents
            .AsNoTracking()
            .Where(document => document.SubjectId == subjectId);

        var documentsByChapter = await _dbContext.Chapters
            .AsNoTracking()
            .Where(chapter => chapter.SubjectId == subjectId)
            .OrderBy(chapter => chapter.Number)
            .Select(chapter => new ReportChapterDocumentCount
            {
                Id = chapter.Id,
                Number = chapter.Number,
                Title = chapter.Title,
                DocumentCount = _dbContext.Documents.Count(document =>
                    document.SubjectId == subjectId && document.ChapterId == chapter.Id)
            })
            .ToListAsync(cancellationToken);

        var statusCounts = await documentsQuery
            .GroupBy(document => document.IndexStatus)
            .Select(group => new
            {
                Status = group.Key,
                Count = group.Count()
            })
            .ToDictionaryAsync(item => item.Status, item => item.Count, cancellationToken);

        var subjectDocumentIds = documentsQuery.Select(document => document.Id);

        var totalChunks = await _dbContext.DocumentChunks
            .AsNoTracking()
            .CountAsync(chunk => subjectDocumentIds.Contains(chunk.DocumentId), cancellationToken);

        var recentFailures = await documentsQuery
            .Where(document => document.IndexStatus == DocumentIndexStatus.Failed)
            .OrderByDescending(document => document.UploadedAtUtc)
            .Take(10)
            .Select(document => new ReportRecentFailure
            {
                DocumentId = document.Id,
                Title = document.Title,
                OriginalFileName = document.OriginalFileName,
                IndexError = document.IndexError,
                UploadedAtUtc = document.UploadedAtUtc
            })
            .ToListAsync(cancellationToken);

        var recentlyIndexed = await documentsQuery
            .Where(document => document.IndexStatus == DocumentIndexStatus.Indexed
                               && document.IndexedAtUtc.HasValue)
            .OrderByDescending(document => document.IndexedAtUtc)
            .Take(10)
            .Select(document => new ReportRecentIndexedDocument
            {
                DocumentId = document.Id,
                Title = document.Title,
                OriginalFileName = document.OriginalFileName,
                IndexedAtUtc = document.IndexedAtUtc!.Value,
                ChunkCount = _dbContext.DocumentChunks.Count(chunk => chunk.DocumentId == document.Id)
            })
            .ToListAsync(cancellationToken);

        var subjectSessionIds = _dbContext.ChatSessions
            .AsNoTracking()
            .Where(session => session.SubjectId == subjectId)
            .Select(session => session.Id);

        var subjectMessageIds = _dbContext.ChatMessages
            .AsNoTracking()
            .Where(message => subjectSessionIds.Contains(message.ChatSessionId))
            .Select(message => message.Id);

        var totalChatSessions = await subjectSessionIds.CountAsync(cancellationToken);
        var totalChatMessages = await subjectMessageIds.CountAsync(cancellationToken);
        var totalMessageCitations = await _dbContext.MessageCitations
            .AsNoTracking()
            .CountAsync(citation => subjectMessageIds.Contains(citation.ChatMessageId), cancellationToken);

        return new SubjectReportSnapshot
        {
            SubjectId = subject.Id,
            SubjectCode = subject.Code,
            SubjectName = subject.Name,
            TotalChapters = documentsByChapter.Count,
            TotalDocuments = statusCounts.Values.Sum(),
            UnassignedDocuments = await documentsQuery.CountAsync(
                document => document.ChapterId == null,
                cancellationToken),
            DocumentsByChapter = documentsByChapter,
            UploadedCount = statusCounts.GetValueOrDefault(DocumentIndexStatus.Uploaded),
            ProcessingCount = statusCounts.GetValueOrDefault(DocumentIndexStatus.Processing),
            IndexedCount = statusCounts.GetValueOrDefault(DocumentIndexStatus.Indexed),
            FailedCount = statusCounts.GetValueOrDefault(DocumentIndexStatus.Failed),
            TotalChunks = totalChunks,
            RecentFailures = recentFailures,
            RecentlyIndexed = recentlyIndexed,
            TotalChatSessions = totalChatSessions,
            TotalChatMessages = totalChatMessages,
            TotalMessageCitations = totalMessageCitations
        };
    }
}
