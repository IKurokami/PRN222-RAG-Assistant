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
    public IReadOnlyList<ReportRecentFailure> RecentFailures { get; init; } = Array.Empty<ReportRecentFailure>();
    public IReadOnlyList<ReportRecentIndexedDocument> RecentlyIndexed { get; init; } = Array.Empty<ReportRecentIndexedDocument>();

    public int TotalChatSessions { get; init; }
    public int TotalChatMessages { get; init; }
    public int TotalMessageCitations { get; init; }
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
