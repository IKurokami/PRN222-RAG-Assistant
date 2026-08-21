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

        var nowUtc = DateTime.UtcNow;
        var activityWindowStartUtc = nowUtc.Date.AddDays(-6);
        var thirtyDayWindowStartUtc = nowUtc.AddDays(-30);

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

        var documentMetadata = await documentsQuery
            .Select(document => new
            {
                document.Id,
                document.Title,
                document.ChapterId,
                document.IndexStatus
            })
            .ToListAsync(cancellationToken);

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

        var subjectSessionsQuery = _dbContext.ChatSessions
            .AsNoTracking()
            .Where(session => session.SubjectId == subjectId);

        var subjectSessionIds = subjectSessionsQuery.Select(session => session.Id);
        var subjectMessagesQuery = _dbContext.ChatMessages
            .AsNoTracking()
            .Where(message => subjectSessionIds.Contains(message.ChatSessionId));

        var totalChatSessions = await subjectSessionsQuery.CountAsync(cancellationToken);
        var totalChatMessages = await subjectMessagesQuery.CountAsync(cancellationToken);
        var userQuestionCount = await subjectMessagesQuery
            .CountAsync(message => message.Role == ChatMessageRole.User, cancellationToken);
        var assistantResponseCount = await subjectMessagesQuery
            .CountAsync(message => message.Role == ChatMessageRole.Assistant, cancellationToken);
        var activeSessionsLast30Days = await subjectMessagesQuery
            .Where(message => message.CreatedAtUtc >= thirtyDayWindowStartUtc)
            .Select(message => message.ChatSessionId)
            .Distinct()
            .CountAsync(cancellationToken);

        var citationRows = await (
            from citation in _dbContext.MessageCitations.AsNoTracking()
            join message in _dbContext.ChatMessages.AsNoTracking()
                on citation.ChatMessageId equals message.Id
            join session in _dbContext.ChatSessions.AsNoTracking()
                on message.ChatSessionId equals session.Id
            join chunk in _dbContext.DocumentChunks.AsNoTracking()
                on citation.DocumentChunkId equals chunk.Id
            join document in _dbContext.Documents.AsNoTracking()
                on chunk.DocumentId equals document.Id
            where session.SubjectId == subjectId && document.SubjectId == subjectId
            select new
            {
                citation.ChatMessageId,
                citation.DocumentChunkId,
                message.ChatSessionId,
                message.Role,
                message.CreatedAtUtc,
                DocumentId = document.Id
            })
            .ToListAsync(cancellationToken);

        var totalMessageCitations = citationRows.Count;
        var assistantCitationRows = citationRows
            .Where(row => row.Role == ChatMessageRole.Assistant)
            .ToList();
        var citedAssistantResponseCount = assistantCitationRows
            .Select(row => row.ChatMessageId)
            .Distinct()
            .Count();

        var documentLookup = documentMetadata.ToDictionary(document => document.Id);
        var chapterLookup = documentsByChapter.ToDictionary(chapter => chapter.Id);

        var topCitedDocuments = citationRows
            .GroupBy(row => row.DocumentId)
            .Select(group =>
            {
                var document = documentLookup[group.Key];
                ReportChapterDocumentCount? chapter = null;
                if (document.ChapterId.HasValue)
                {
                    chapterLookup.TryGetValue(document.ChapterId.Value, out chapter);
                }

                return new ReportTopCitedDocument
                {
                    DocumentId = document.Id,
                    Title = document.Title,
                    ChapterNumber = chapter?.Number,
                    ChapterTitle = chapter?.Title,
                    CitationCount = group.Count(),
                    DistinctSessions = group.Select(row => row.ChatSessionId).Distinct().Count(),
                    CitedChunkCount = group.Select(row => row.DocumentChunkId).Distinct().Count()
                };
            })
            .OrderByDescending(item => item.CitationCount)
            .ThenByDescending(item => item.DistinctSessions)
            .ThenBy(item => item.Title)
            .Take(10)
            .ToList();

        var topCitedChapters = citationRows
            .Select(row => new
            {
                Row = row,
                ChapterId = documentLookup[row.DocumentId].ChapterId
            })
            .GroupBy(item => item.ChapterId)
            .Select(group =>
            {
                if (group.Key.HasValue && chapterLookup.TryGetValue(group.Key.Value, out var chapter))
                {
                    return new ReportTopCitedChapter
                    {
                        ChapterId = chapter.Id,
                        Number = chapter.Number,
                        Title = chapter.Title,
                        DocumentCount = chapter.DocumentCount,
                        CitedDocumentCount = group.Select(item => item.Row.DocumentId).Distinct().Count(),
                        CitationCount = group.Count()
                    };
                }

                return new ReportTopCitedChapter
                {
                    ChapterId = null,
                    Number = null,
                    Title = "Tài liệu chưa gán chương",
                    DocumentCount = documentMetadata.Count(document => document.ChapterId == null),
                    CitedDocumentCount = group.Select(item => item.Row.DocumentId).Distinct().Count(),
                    CitationCount = group.Count()
                };
            })
            .OrderByDescending(item => item.CitationCount)
            .ThenBy(item => item.Number ?? int.MaxValue)
            .Take(10)
            .ToList();

        var recentMessages = await subjectMessagesQuery
            .Where(message => message.CreatedAtUtc >= activityWindowStartUtc)
            .Select(message => new
            {
                message.Id,
                message.ChatSessionId,
                message.Role,
                message.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var activeSessionsLast7Days = recentMessages
            .Select(message => message.ChatSessionId)
            .Distinct()
            .Count();

        var dailyActivityLast7Days = Enumerable.Range(0, 7)
            .Select(dayOffset =>
            {
                var dayStartUtc = activityWindowStartUtc.AddDays(dayOffset);
                var nextDayUtc = dayStartUtc.AddDays(1);

                return new ReportDailyChatActivity
                {
                    DateUtc = dayStartUtc,
                    UserMessages = recentMessages.Count(message =>
                        message.Role == ChatMessageRole.User
                        && message.CreatedAtUtc >= dayStartUtc
                        && message.CreatedAtUtc < nextDayUtc),
                    AssistantMessages = recentMessages.Count(message =>
                        message.Role == ChatMessageRole.Assistant
                        && message.CreatedAtUtc >= dayStartUtc
                        && message.CreatedAtUtc < nextDayUtc),
                    CitationCount = citationRows.Count(row =>
                        row.CreatedAtUtc >= dayStartUtc
                        && row.CreatedAtUtc < nextDayUtc)
                };
            })
            .ToList();

        var indexedDocumentIds = documentMetadata
            .Where(document => document.IndexStatus == DocumentIndexStatus.Indexed)
            .Select(document => document.Id)
            .ToHashSet();
        var citedIndexedDocumentIds = citationRows
            .Select(row => row.DocumentId)
            .Where(indexedDocumentIds.Contains)
            .Distinct()
            .ToHashSet();

        var indexedCount = statusCounts.GetValueOrDefault(DocumentIndexStatus.Indexed);
        var topThreeCitationCount = topCitedDocuments
            .Take(3)
            .Sum(document => document.CitationCount);

        static double Ratio(int numerator, int denominator)
        {
            return denominator == 0
                ? 0
                : Math.Round(numerator * 100.0 / denominator, 1, MidpointRounding.AwayFromZero);
        }

        static double Average(int numerator, int denominator)
        {
            return denominator == 0
                ? 0
                : Math.Round(numerator * 1.0 / denominator, 2, MidpointRounding.AwayFromZero);
        }

        return new SubjectReportSnapshot
        {
            SubjectId = subject.Id,
            SubjectCode = subject.Code,
            SubjectName = subject.Name,
            TotalChapters = documentsByChapter.Count,
            TotalDocuments = statusCounts.Values.Sum(),
            UnassignedDocuments = documentMetadata.Count(document => document.ChapterId == null),
            DocumentsByChapter = documentsByChapter,
            UploadedCount = statusCounts.GetValueOrDefault(DocumentIndexStatus.Uploaded),
            ProcessingCount = statusCounts.GetValueOrDefault(DocumentIndexStatus.Processing),
            IndexedCount = indexedCount,
            FailedCount = statusCounts.GetValueOrDefault(DocumentIndexStatus.Failed),
            TotalChunks = totalChunks,
            AverageChunksPerIndexedDocument = Average(totalChunks, indexedCount),
            RecentFailures = recentFailures,
            RecentlyIndexed = recentlyIndexed,
            TotalChatSessions = totalChatSessions,
            TotalChatMessages = totalChatMessages,
            TotalMessageCitations = totalMessageCitations,
            UserQuestionCount = userQuestionCount,
            AssistantResponseCount = assistantResponseCount,
            CitedAssistantResponseCount = citedAssistantResponseCount,
            ActiveSessionsLast7Days = activeSessionsLast7Days,
            ActiveSessionsLast30Days = activeSessionsLast30Days,
            AverageMessagesPerSession = Average(totalChatMessages, totalChatSessions),
            AverageCitationsPerAssistantResponse = Average(assistantCitationRows.Count, assistantResponseCount),
            CitationCoveragePercent = Ratio(citedAssistantResponseCount, assistantResponseCount),
            UniqueCitedDocuments = citationRows.Select(row => row.DocumentId).Distinct().Count(),
            IndexedButNeverCitedDocuments = indexedDocumentIds.Count - citedIndexedDocumentIds.Count,
            CitedDocumentCoveragePercent = Ratio(citedIndexedDocumentIds.Count, indexedDocumentIds.Count),
            TopThreeCitationSharePercent = Ratio(topThreeCitationCount, totalMessageCitations),
            TopCitedDocuments = topCitedDocuments,
            TopCitedChapters = topCitedChapters,
            DailyActivityLast7Days = dailyActivityLast7Days
        };
    }
}
