using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Application.Models;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Pages.Reports;

[Authorize(Policy = AppPolicies.ManageDocuments)]
public sealed class IndexModel : PageModel
{
    private readonly IReportQueryService _reportQueryService;
    private readonly ISubjectAccessService _subjectAccessService;

    public IndexModel(
        IReportQueryService reportQueryService,
        ISubjectAccessService subjectAccessService)
    {
        _reportQueryService = reportQueryService;
        _subjectAccessService = subjectAccessService;
    }

    public Guid SubjectId { get; private set; }
    public string SubjectCode { get; private set; } = string.Empty;
    public string SubjectName { get; private set; } = string.Empty;

    public int TotalChapters { get; private set; }
    public int TotalDocuments { get; private set; }
    public int UnassignedDocuments { get; private set; }
    public IReadOnlyList<ReportChapterDocumentCount> DocumentsByChapter { get; private set; } = Array.Empty<ReportChapterDocumentCount>();

    public int UploadedCount { get; private set; }
    public int ProcessingCount { get; private set; }
    public int IndexedCount { get; private set; }
    public int FailedCount { get; private set; }
    public int TotalChunks { get; private set; }
    public double AverageChunksPerIndexedDocument { get; private set; }
    public IReadOnlyList<ReportRecentFailure> RecentFailures { get; private set; } = Array.Empty<ReportRecentFailure>();
    public IReadOnlyList<ReportRecentIndexedDocument> RecentlyIndexed { get; private set; } = Array.Empty<ReportRecentIndexedDocument>();

    public int TotalChatSessions { get; private set; }
    public int TotalChatMessages { get; private set; }
    public int TotalMessageCitations { get; private set; }
    public int UserQuestionCount { get; private set; }
    public int AssistantResponseCount { get; private set; }
    public int CitedAssistantResponseCount { get; private set; }
    public int ActiveSessionsLast7Days { get; private set; }
    public int ActiveSessionsLast30Days { get; private set; }
    public double AverageMessagesPerSession { get; private set; }
    public double AverageCitationsPerAssistantResponse { get; private set; }
    public double CitationCoveragePercent { get; private set; }

    public int UniqueCitedDocuments { get; private set; }
    public int IndexedButNeverCitedDocuments { get; private set; }
    public double CitedDocumentCoveragePercent { get; private set; }
    public double TopThreeCitationSharePercent { get; private set; }
    public IReadOnlyList<ReportTopCitedDocument> TopCitedDocuments { get; private set; } = Array.Empty<ReportTopCitedDocument>();
    public IReadOnlyList<ReportTopCitedChapter> TopCitedChapters { get; private set; } = Array.Empty<ReportTopCitedChapter>();
    public IReadOnlyList<ReportDailyChatActivity> DailyActivityLast7Days { get; private set; } = Array.Empty<ReportDailyChatActivity>();

    public async Task<IActionResult> OnGetAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        if (subjectId == Guid.Empty)
        {
            return Redirect("/subjects");
        }

        if (!await _subjectAccessService.CanManageSubjectAsync(User, subjectId, cancellationToken))
        {
            return Forbid();
        }

        var report = await _reportQueryService.GetSubjectReportAsync(subjectId, cancellationToken);
        if (report is null)
        {
            return NotFound();
        }

        Apply(report);
        return Page();
    }

    private void Apply(SubjectReportSnapshot report)
    {
        SubjectId = report.SubjectId;
        SubjectCode = report.SubjectCode;
        SubjectName = report.SubjectName;
        TotalChapters = report.TotalChapters;
        TotalDocuments = report.TotalDocuments;
        UnassignedDocuments = report.UnassignedDocuments;
        DocumentsByChapter = report.DocumentsByChapter;
        UploadedCount = report.UploadedCount;
        ProcessingCount = report.ProcessingCount;
        IndexedCount = report.IndexedCount;
        FailedCount = report.FailedCount;
        TotalChunks = report.TotalChunks;
        AverageChunksPerIndexedDocument = report.AverageChunksPerIndexedDocument;
        RecentFailures = report.RecentFailures;
        RecentlyIndexed = report.RecentlyIndexed;
        TotalChatSessions = report.TotalChatSessions;
        TotalChatMessages = report.TotalChatMessages;
        TotalMessageCitations = report.TotalMessageCitations;
        UserQuestionCount = report.UserQuestionCount;
        AssistantResponseCount = report.AssistantResponseCount;
        CitedAssistantResponseCount = report.CitedAssistantResponseCount;
        ActiveSessionsLast7Days = report.ActiveSessionsLast7Days;
        ActiveSessionsLast30Days = report.ActiveSessionsLast30Days;
        AverageMessagesPerSession = report.AverageMessagesPerSession;
        AverageCitationsPerAssistantResponse = report.AverageCitationsPerAssistantResponse;
        CitationCoveragePercent = report.CitationCoveragePercent;
        UniqueCitedDocuments = report.UniqueCitedDocuments;
        IndexedButNeverCitedDocuments = report.IndexedButNeverCitedDocuments;
        CitedDocumentCoveragePercent = report.CitedDocumentCoveragePercent;
        TopThreeCitationSharePercent = report.TopThreeCitationSharePercent;
        TopCitedDocuments = report.TopCitedDocuments;
        TopCitedChapters = report.TopCitedChapters;
        DailyActivityLast7Days = report.DailyActivityLast7Days;
    }
}
