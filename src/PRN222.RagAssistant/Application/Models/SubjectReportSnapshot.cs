namespace PRN222.RagAssistant.Application.Models;

public sealed class SubjectReportSnapshot
{
    public Guid SubjectId { get; init; }
    public string SubjectCode { get; init; } = string.Empty;
    public string SubjectName { get; init; } = string.Empty;

    public int TotalChapters { get; init; }
    public int TotalDocuments { get; init; }
    public int UnassignedDocuments { get; init; }
    public IReadOnlyList<ReportChapterDocumentCount> DocumentsByChapter { get; init; } = Array.Empty<ReportChapterDocumentCount>();

    public int UploadedCount { get; init; }
    public int ProcessingCount { get; init; }
    public int IndexedCount { get; init; }
    public int FailedCount { get; init; }
    public int TotalChunks { get; init; }
    public double AverageChunksPerIndexedDocument { get; init; }
    public IReadOnlyList<ReportRecentFailure> RecentFailures { get; init; } = Array.Empty<ReportRecentFailure>();
    public IReadOnlyList<ReportRecentIndexedDocument> RecentlyIndexed { get; init; } = Array.Empty<ReportRecentIndexedDocument>();

    public int TotalChatSessions { get; init; }
    public int TotalChatMessages { get; init; }
    public int TotalMessageCitations { get; init; }
    public int UserQuestionCount { get; init; }
    public int AssistantResponseCount { get; init; }
    public int CitedAssistantResponseCount { get; init; }
    public int ActiveSessionsLast7Days { get; init; }
    public int ActiveSessionsLast30Days { get; init; }
    public double AverageMessagesPerSession { get; init; }
    public double AverageCitationsPerAssistantResponse { get; init; }
    public double CitationCoveragePercent { get; init; }

    public int UniqueCitedDocuments { get; init; }
    public int IndexedButNeverCitedDocuments { get; init; }
    public double CitedDocumentCoveragePercent { get; init; }
    public double TopThreeCitationSharePercent { get; init; }
    public IReadOnlyList<ReportTopCitedDocument> TopCitedDocuments { get; init; } = Array.Empty<ReportTopCitedDocument>();
    public IReadOnlyList<ReportTopCitedChapter> TopCitedChapters { get; init; } = Array.Empty<ReportTopCitedChapter>();
    public IReadOnlyList<ReportDailyChatActivity> DailyActivityLast7Days { get; init; } = Array.Empty<ReportDailyChatActivity>();
}

public sealed class ReportChapterDocumentCount
{
    public Guid Id { get; init; }
    public int Number { get; init; }
    public string Title { get; init; } = string.Empty;
    public int DocumentCount { get; init; }
}

public sealed class ReportRecentFailure
{
    public Guid DocumentId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string OriginalFileName { get; init; } = string.Empty;
    public string? IndexError { get; init; }
    public DateTime UploadedAtUtc { get; init; }
}

public sealed class ReportRecentIndexedDocument
{
    public Guid DocumentId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string OriginalFileName { get; init; } = string.Empty;
    public DateTime IndexedAtUtc { get; init; }
    public int ChunkCount { get; init; }
}

public sealed class ReportTopCitedDocument
{
    public Guid DocumentId { get; init; }
    public string Title { get; init; } = string.Empty;
    public int? ChapterNumber { get; init; }
    public string? ChapterTitle { get; init; }
    public int CitationCount { get; init; }
    public int DistinctSessions { get; init; }
    public int CitedChunkCount { get; init; }
}

public sealed class ReportTopCitedChapter
{
    public Guid? ChapterId { get; init; }
    public int? Number { get; init; }
    public string Title { get; init; } = string.Empty;
    public int DocumentCount { get; init; }
    public int CitedDocumentCount { get; init; }
    public int CitationCount { get; init; }
}

public sealed class ReportDailyChatActivity
{
    public DateTime DateUtc { get; init; }
    public int UserMessages { get; init; }
    public int AssistantMessages { get; init; }
    public int CitationCount { get; init; }
}
