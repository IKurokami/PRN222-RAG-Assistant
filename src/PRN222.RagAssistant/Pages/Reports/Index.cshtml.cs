using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Enums;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Pages.Reports;

[Authorize(Policy = AppPolicies.ManageDocuments)]
public sealed class IndexModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ISubjectAccessService _subjectAccessService;

    public IndexModel(
        ApplicationDbContext dbContext,
        ISubjectAccessService subjectAccessService)
    {
        _dbContext = dbContext;
        _subjectAccessService = subjectAccessService;
    }

    public Guid SubjectId { get; set; }
    public string SubjectCode { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;

    public int TotalChapters { get; set; }
    public int TotalDocuments { get; set; }
    public int UnassignedDocuments { get; set; }
    public List<ChapterDocumentCountViewModel> DocumentsByChapter { get; set; } = [];

    public int UploadedCount { get; set; }
    public int ProcessingCount { get; set; }
    public int IndexedCount { get; set; }
    public int FailedCount { get; set; }
    public int TotalChunks { get; set; }
    public List<RecentFailureViewModel> RecentFailures { get; set; } = [];
    public List<RecentIndexedViewModel> RecentlyIndexed { get; set; } = [];

    public int TotalChatSessions { get; set; }
    public int TotalChatMessages { get; set; }
    public int TotalMessageCitations { get; set; }

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

        var subject = await _dbContext.Subjects
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == subjectId, cancellationToken);

        if (subject is null)
        {
            return NotFound();
        }

        SubjectId = subject.Id;
        SubjectCode = subject.Code;
        SubjectName = subject.Name;

        TotalChapters = await _dbContext.Chapters
            .AsNoTracking()
            .CountAsync(chapter => chapter.SubjectId == subjectId, cancellationToken);

        TotalDocuments = await _dbContext.Documents
            .AsNoTracking()
            .CountAsync(document => document.SubjectId == subjectId, cancellationToken);

        UnassignedDocuments = await _dbContext.Documents
            .AsNoTracking()
            .CountAsync(document => document.SubjectId == subjectId && document.ChapterId == null, cancellationToken);

        var chapters = await _dbContext.Chapters
            .AsNoTracking()
            .Where(chapter => chapter.SubjectId == subjectId)
            .OrderBy(chapter => chapter.Number)
            .ToListAsync(cancellationToken);

        var chapterIds = chapters.Select(chapter => chapter.Id).ToList();
        var docCountsByChapter = await _dbContext.Documents
            .AsNoTracking()
            .Where(document => document.SubjectId == subjectId
                               && document.ChapterId.HasValue
                               && chapterIds.Contains(document.ChapterId.Value))
            .GroupBy(document => document.ChapterId!.Value)
            .Select(group => new { ChapterId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var countMap = docCountsByChapter.ToDictionary(item => item.ChapterId, item => item.Count);
        DocumentsByChapter = chapters.Select(chapter => new ChapterDocumentCountViewModel
        {
            Id = chapter.Id,
            Number = chapter.Number,
            Title = chapter.Title,
            DocumentCount = countMap.GetValueOrDefault(chapter.Id)
        }).ToList();

        var statusCounts = await _dbContext.Documents
            .AsNoTracking()
            .Where(document => document.SubjectId == subjectId)
            .GroupBy(document => document.IndexStatus)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        foreach (var item in statusCounts)
        {
            switch (item.Status)
            {
                case DocumentIndexStatus.Uploaded:
                    UploadedCount = item.Count;
                    break;
                case DocumentIndexStatus.Processing:
                    ProcessingCount = item.Count;
                    break;
                case DocumentIndexStatus.Indexed:
                    IndexedCount = item.Count;
                    break;
                case DocumentIndexStatus.Failed:
                    FailedCount = item.Count;
                    break;
            }
        }

        var subjectDocumentIds = await _dbContext.Documents
            .AsNoTracking()
            .Where(document => document.SubjectId == subjectId)
            .Select(document => document.Id)
            .ToListAsync(cancellationToken);

        TotalChunks = await _dbContext.DocumentChunks
            .AsNoTracking()
            .CountAsync(chunk => subjectDocumentIds.Contains(chunk.DocumentId), cancellationToken);

        RecentFailures = await _dbContext.Documents
            .AsNoTracking()
            .Where(document => document.SubjectId == subjectId && document.IndexStatus == DocumentIndexStatus.Failed)
            .OrderByDescending(document => document.UploadedAtUtc)
            .Take(10)
            .Select(document => new RecentFailureViewModel
            {
                DocumentId = document.Id,
                Title = document.Title,
                OriginalFileName = document.OriginalFileName,
                IndexError = document.IndexError,
                UploadedAtUtc = document.UploadedAtUtc
            })
            .ToListAsync(cancellationToken);

        var recentlyIndexedDocs = await _dbContext.Documents
            .AsNoTracking()
            .Where(document => document.SubjectId == subjectId
                               && document.IndexStatus == DocumentIndexStatus.Indexed
                               && document.IndexedAtUtc.HasValue)
            .OrderByDescending(document => document.IndexedAtUtc)
            .Take(10)
            .ToListAsync(cancellationToken);

        var recentDocIds = recentlyIndexedDocs.Select(document => document.Id).ToList();
        var chunkCountPerDoc = await _dbContext.DocumentChunks
            .AsNoTracking()
            .Where(chunk => recentDocIds.Contains(chunk.DocumentId))
            .GroupBy(chunk => chunk.DocumentId)
            .Select(group => new { DocumentId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var chunkCountMap = chunkCountPerDoc.ToDictionary(item => item.DocumentId, item => item.Count);
        RecentlyIndexed = recentlyIndexedDocs.Select(document => new RecentIndexedViewModel
        {
            DocumentId = document.Id,
            Title = document.Title,
            OriginalFileName = document.OriginalFileName,
            IndexedAtUtc = document.IndexedAtUtc!.Value,
            ChunkCount = chunkCountMap.GetValueOrDefault(document.Id)
        }).ToList();

        TotalChatSessions = await _dbContext.ChatSessions
            .AsNoTracking()
            .CountAsync(session => session.SubjectId == subjectId, cancellationToken);

        var sessionIdsForSubject = _dbContext.ChatSessions
            .AsNoTracking()
            .Where(session => session.SubjectId == subjectId)
            .Select(session => session.Id);

        TotalChatMessages = await _dbContext.ChatMessages
            .AsNoTracking()
            .Where(message => sessionIdsForSubject.Contains(message.ChatSessionId))
            .CountAsync(cancellationToken);

        var messageIdsForSubject = _dbContext.ChatMessages
            .AsNoTracking()
            .Where(message => sessionIdsForSubject.Contains(message.ChatSessionId))
            .Select(message => message.Id);

        TotalMessageCitations = await _dbContext.MessageCitations
            .AsNoTracking()
            .Where(citation => messageIdsForSubject.Contains(citation.ChatMessageId))
            .CountAsync(cancellationToken);

        return Page();
    }

    public sealed class ChapterDocumentCountViewModel
    {
        public Guid Id { get; set; }
        public int Number { get; set; }
        public string Title { get; set; } = string.Empty;
        public int DocumentCount { get; set; }
    }

    public sealed class RecentFailureViewModel
    {
        public Guid DocumentId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public string? IndexError { get; set; }
        public DateTime UploadedAtUtc { get; set; }
    }

    public sealed class RecentIndexedViewModel
    {
        public Guid DocumentId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public DateTime IndexedAtUtc { get; set; }
        public int ChunkCount { get; set; }
    }
}
