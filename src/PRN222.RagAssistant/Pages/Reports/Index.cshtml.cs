using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Data.Seed;
using PRN222.RagAssistant.Domain.Enums;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Pages.Reports;

[Authorize(Policy = AppPolicies.ManageDocuments)]
public sealed class IndexModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;

    public IndexModel(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // ─── Thống kê Chương & Tài liệu ─────────────────────────────────────────

    public int TotalChapters { get; set; }
    public int TotalDocuments { get; set; }
    public int UnassignedDocuments { get; set; }

    public List<ChapterDocumentCountViewModel> DocumentsByChapter { get; set; } = [];

    // ─── Thống kê Indexing ───────────────────────────────────────────────────

    public int UploadedCount { get; set; }
    public int ProcessingCount { get; set; }
    public int IndexedCount { get; set; }
    public int FailedCount { get; set; }
    public int TotalChunks { get; set; }

    public List<RecentFailureViewModel> RecentFailures { get; set; } = [];
    public List<RecentIndexedViewModel> RecentlyIndexed { get; set; } = [];

    // ─── Thống kê Chat ───────────────────────────────────────────────────────

    public int TotalChatSessions { get; set; }
    public int TotalChatMessages { get; set; }
    public int TotalMessageCitations { get; set; }

    // ─── Handler ─────────────────────────────────────────────────────────────

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        // 1. Thống kê Chương & Tài liệu
        TotalChapters = await _dbContext.Chapters
            .AsNoTracking()
            .CountAsync(c => c.SubjectId == SeedData.Prn222SubjectId, cancellationToken);

        TotalDocuments = await _dbContext.Documents
            .AsNoTracking()
            .CountAsync(d => d.SubjectId == SeedData.Prn222SubjectId, cancellationToken);

        UnassignedDocuments = await _dbContext.Documents
            .AsNoTracking()
            .CountAsync(d => d.SubjectId == SeedData.Prn222SubjectId && d.ChapterId == null, cancellationToken);

        // Tài liệu theo từng chương
        var chapters = await _dbContext.Chapters
            .AsNoTracking()
            .Where(c => c.SubjectId == SeedData.Prn222SubjectId)
            .OrderBy(c => c.Number)
            .ToListAsync(cancellationToken);

        var chapterIds = chapters.Select(c => c.Id).ToList();

        var docCountsByChapter = await _dbContext.Documents
            .AsNoTracking()
            .Where(d => d.SubjectId == SeedData.Prn222SubjectId && d.ChapterId.HasValue && chapterIds.Contains(d.ChapterId!.Value))
            .GroupBy(d => d.ChapterId!.Value)
            .Select(g => new { ChapterId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var countMap = docCountsByChapter.ToDictionary(x => x.ChapterId, x => x.Count);

        DocumentsByChapter = chapters.Select(c => new ChapterDocumentCountViewModel
        {
            Number = c.Number,
            Title = c.Title,
            DocumentCount = countMap.TryGetValue(c.Id, out var cnt) ? cnt : 0
        }).ToList();

        // 2. Thống kê Indexing
        var statusCounts = await _dbContext.Documents
            .AsNoTracking()
            .Where(d => d.SubjectId == SeedData.Prn222SubjectId)
            .GroupBy(d => d.IndexStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        foreach (var item in statusCounts)
        {
            switch (item.Status)
            {
                case DocumentIndexStatus.Uploaded:   UploadedCount   = item.Count; break;
                case DocumentIndexStatus.Processing: ProcessingCount = item.Count; break;
                case DocumentIndexStatus.Indexed:    IndexedCount    = item.Count; break;
                case DocumentIndexStatus.Failed:     FailedCount     = item.Count; break;
            }
        }

        // Tổng số chunks từ tài liệu của môn PRN222
        var prn222DocumentIds = await _dbContext.Documents
            .AsNoTracking()
            .Where(d => d.SubjectId == SeedData.Prn222SubjectId)
            .Select(d => d.Id)
            .ToListAsync(cancellationToken);

        TotalChunks = await _dbContext.DocumentChunks
            .AsNoTracking()
            .CountAsync(c => prn222DocumentIds.Contains(c.DocumentId), cancellationToken);

        // Danh sách tài liệu lỗi gần đây (tối đa 10)
        RecentFailures = await _dbContext.Documents
            .AsNoTracking()
            .Where(d => d.SubjectId == SeedData.Prn222SubjectId && d.IndexStatus == DocumentIndexStatus.Failed)
            .OrderByDescending(d => d.UploadedAtUtc)
            .Take(10)
            .Select(d => new RecentFailureViewModel
            {
                DocumentId = d.Id,
                Title = d.Title,
                OriginalFileName = d.OriginalFileName,
                IndexError = d.IndexError,
                UploadedAtUtc = d.UploadedAtUtc
            })
            .ToListAsync(cancellationToken);

        // Danh sách tài liệu đã index thành công gần đây (tối đa 10)
        var recentlyIndexedDocs = await _dbContext.Documents
            .AsNoTracking()
            .Where(d => d.SubjectId == SeedData.Prn222SubjectId && d.IndexStatus == DocumentIndexStatus.Indexed && d.IndexedAtUtc.HasValue)
            .OrderByDescending(d => d.IndexedAtUtc)
            .Take(10)
            .ToListAsync(cancellationToken);

        var recentDocIds = recentlyIndexedDocs.Select(d => d.Id).ToList();

        var chunkCountPerDoc = await _dbContext.DocumentChunks
            .AsNoTracking()
            .Where(c => recentDocIds.Contains(c.DocumentId))
            .GroupBy(c => c.DocumentId)
            .Select(g => new { DocumentId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var chunkCountMap = chunkCountPerDoc.ToDictionary(x => x.DocumentId, x => x.Count);

        RecentlyIndexed = recentlyIndexedDocs.Select(d => new RecentIndexedViewModel
        {
            DocumentId = d.Id,
            Title = d.Title,
            OriginalFileName = d.OriginalFileName,
            IndexedAtUtc = d.IndexedAtUtc!.Value,
            ChunkCount = chunkCountMap.TryGetValue(d.Id, out var cc) ? cc : 0
        }).ToList();

        // 3. Thống kê Chat (sẽ trả về 0 khi Flow 2 chưa có dữ liệu)
        TotalChatSessions = await _dbContext.ChatSessions.AsNoTracking().CountAsync(cancellationToken);
        TotalChatMessages = await _dbContext.ChatMessages.AsNoTracking().CountAsync(cancellationToken);
        TotalMessageCitations = await _dbContext.MessageCitations.AsNoTracking().CountAsync(cancellationToken);
    }

    // ─── ViewModels ──────────────────────────────────────────────────────────

    public sealed class ChapterDocumentCountViewModel
    {
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
