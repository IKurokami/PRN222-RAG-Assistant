using Microsoft.AspNetCore.Authorization;
using PRN222.RagAssistant.Data.Seed;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Domain.Enums;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Tests;

/// <summary>
/// Tests cho Flow 3 - Report &amp; Statistics.
///
/// Bao gồm kiểm tra:
/// - Phân quyền attribute trên trang Reports (phải có ManageDocuments policy).
/// - Tính đúng đắn của logic thống kê qua LINQ-to-objects.
/// - Trạng thái rỗng (empty/zero state) khi chưa có dữ liệu.
/// - Tính chất read-only (không làm thay đổi dữ liệu).
///
/// DB tests sử dụng danh sách in-memory (LINQ-to-objects) nhất quán với
/// pattern của các test hiện có trong dự án.
/// </summary>
public sealed class ReportStatisticsTests
{
    // ─── Kiểm tra Phân quyền Attribute ──────────────────────────────────────

    [Fact]
    public void Reports_Index_page_requires_ManageDocuments_policy()
    {
        var pageModelType = typeof(Pages.Reports.IndexModel);

        var authorizeAttrs = pageModelType
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .ToList();

        Assert.True(
            authorizeAttrs.Any(),
            "Reports.IndexModel phải có ít nhất một thuộc tính [Authorize].");

        var hasManageDocuments = authorizeAttrs
            .Any(a => a.Policy == AppPolicies.ManageDocuments);

        Assert.True(
            hasManageDocuments,
            $"Reports.IndexModel phải có [Authorize(Policy = \"{AppPolicies.ManageDocuments}\")] " +
            "để ngăn truy cập bởi Student và người dùng ẩn danh.");
    }

    [Fact]
    public void Reports_Index_page_does_not_allow_student_access_via_attribute()
    {
        var pageModelType = typeof(Pages.Reports.IndexModel);

        var authorizeAttrs = pageModelType
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .ToList();

        // Không có attribute nào với Roles = "Student"
        var allowsStudentRole = authorizeAttrs
            .Any(a => a.Roles != null && a.Roles.Contains(AppRoles.Student));

        Assert.False(allowsStudentRole,
            "Reports.IndexModel không được khai báo [Authorize(Roles = Student)] trực tiếp.");
    }

    // ─── Kiểm tra với danh sách rỗng (Empty State) ──────────────────────────

    [Fact]
    public void Empty_collections_return_all_zero_counts()
    {
        var chapters = new List<Chapter>();
        var documents = new List<Document>();
        var chunks = new List<DocumentChunk>();
        var sessions = new List<ChatSession>();
        var messages = new List<ChatMessage>();
        var citations = new List<MessageCitation>();

        var result = CalculateReport(chapters, documents, chunks, sessions, messages, citations);

        Assert.Equal(0, result.TotalChapters);
        Assert.Equal(0, result.TotalDocuments);
        Assert.Equal(0, result.UnassignedDocuments);
        Assert.Equal(0, result.TotalChunks);
        Assert.Equal(0, result.UploadedCount);
        Assert.Equal(0, result.ProcessingCount);
        Assert.Equal(0, result.IndexedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(0, result.TotalChatSessions);
        Assert.Equal(0, result.TotalChatMessages);
        Assert.Equal(0, result.TotalMessageCitations);
        Assert.Empty(result.DocumentsByChapter);
        Assert.Empty(result.RecentFailures);
    }

    // ─── Kiểm tra Tổng số Chương & Tài liệu ─────────────────────────────────

    [Fact]
    public void Correctly_counts_chapters_only_for_prn222_subject()
    {
        var chapters = new List<Chapter>
        {
            new Chapter { Id = Guid.NewGuid(), SubjectId = SeedData.Prn222SubjectId, Number = 1, Title = "Giới thiệu C#" },
            new Chapter { Id = Guid.NewGuid(), SubjectId = SeedData.Prn222SubjectId, Number = 2, Title = "ASP.NET Core" },
            // Chương thuộc môn khác — không được đếm
            new Chapter { Id = Guid.NewGuid(), SubjectId = Guid.NewGuid(), Number = 1, Title = "Chương khác môn" }
        };

        var result = CalculateReport(chapters, [], [], [], [], []);

        Assert.Equal(2, result.TotalChapters);
    }

    [Fact]
    public void Correctly_counts_documents_only_for_prn222_subject()
    {
        var ch1Id = Guid.NewGuid();
        var documents = new List<Document>
        {
            CreateDocument(ch1Id, DocumentIndexStatus.Indexed),
            CreateDocument(null, DocumentIndexStatus.Uploaded),
            // Tài liệu thuộc môn khác — không được đếm
            CreateDocument(null, DocumentIndexStatus.Indexed, subjectId: Guid.NewGuid())
        };

        var result = CalculateReport([], documents, [], [], [], []);

        Assert.Equal(2, result.TotalDocuments);
    }

    [Fact]
    public void Correctly_counts_unassigned_documents()
    {
        var chapterId = Guid.NewGuid();
        var documents = new List<Document>
        {
            CreateDocument(chapterId, DocumentIndexStatus.Indexed),  // có gán chương
            CreateDocument(null, DocumentIndexStatus.Uploaded),       // chưa gán chương
            CreateDocument(null, DocumentIndexStatus.Failed)          // chưa gán chương
        };

        var result = CalculateReport([], documents, [], [], [], []);

        Assert.Equal(2, result.UnassignedDocuments);
    }

    // ─── Kiểm tra Phân nhóm theo Chương ──────────────────────────────────────

    [Fact]
    public void DocumentsByChapter_groups_document_counts_correctly()
    {
        var ch1 = new Chapter { Id = Guid.NewGuid(), SubjectId = SeedData.Prn222SubjectId, Number = 1, Title = "Chương 1" };
        var ch2 = new Chapter { Id = Guid.NewGuid(), SubjectId = SeedData.Prn222SubjectId, Number = 2, Title = "Chương 2" };

        var documents = new List<Document>
        {
            CreateDocument(ch1.Id, DocumentIndexStatus.Indexed),
            CreateDocument(ch1.Id, DocumentIndexStatus.Uploaded),
            CreateDocument(ch2.Id, DocumentIndexStatus.Indexed)
        };

        var result = CalculateReport([ch1, ch2], documents, [], [], [], []);

        Assert.Equal(2, result.DocumentsByChapter.Count);

        var ch1Row = result.DocumentsByChapter.First(r => r.Number == 1);
        Assert.Equal(2, ch1Row.DocumentCount);

        var ch2Row = result.DocumentsByChapter.First(r => r.Number == 2);
        Assert.Equal(1, ch2Row.DocumentCount);
    }

    [Fact]
    public void DocumentsByChapter_shows_zero_for_chapters_with_no_documents()
    {
        var ch1 = new Chapter { Id = Guid.NewGuid(), SubjectId = SeedData.Prn222SubjectId, Number = 1, Title = "Chương trống" };

        var result = CalculateReport([ch1], [], [], [], [], []);

        Assert.Single(result.DocumentsByChapter);
        Assert.Equal(0, result.DocumentsByChapter[0].DocumentCount);
    }

    [Fact]
    public void DocumentsByChapter_is_ordered_by_chapter_number_ascending()
    {
        // Thêm các chương theo thứ tự ngẫu nhiên
        var ch3 = new Chapter { Id = Guid.NewGuid(), SubjectId = SeedData.Prn222SubjectId, Number = 3, Title = "Chương 3" };
        var ch1 = new Chapter { Id = Guid.NewGuid(), SubjectId = SeedData.Prn222SubjectId, Number = 1, Title = "Chương 1" };
        var ch2 = new Chapter { Id = Guid.NewGuid(), SubjectId = SeedData.Prn222SubjectId, Number = 2, Title = "Chương 2" };

        var result = CalculateReport([ch3, ch1, ch2], [], [], [], [], []);

        Assert.Equal(3, result.DocumentsByChapter.Count);
        Assert.Equal(1, result.DocumentsByChapter[0].Number);
        Assert.Equal(2, result.DocumentsByChapter[1].Number);
        Assert.Equal(3, result.DocumentsByChapter[2].Number);
    }

    // ─── Kiểm tra phân loại theo DocumentIndexStatus ─────────────────────────

    [Fact]
    public void Correctly_counts_documents_by_index_status()
    {
        var documents = new List<Document>
        {
            CreateDocument(null, DocumentIndexStatus.Uploaded),
            CreateDocument(null, DocumentIndexStatus.Uploaded),
            CreateDocument(null, DocumentIndexStatus.Processing),
            CreateDocument(null, DocumentIndexStatus.Indexed),
            CreateDocument(null, DocumentIndexStatus.Indexed),
            CreateDocument(null, DocumentIndexStatus.Indexed),
            CreateDocument(null, DocumentIndexStatus.Failed)
        };

        var result = CalculateReport([], documents, [], [], [], []);

        Assert.Equal(2, result.UploadedCount);
        Assert.Equal(1, result.ProcessingCount);
        Assert.Equal(3, result.IndexedCount);
        Assert.Equal(1, result.FailedCount);
    }

    [Fact]
    public void Status_counts_exclude_documents_from_other_subjects()
    {
        var documents = new List<Document>
        {
            CreateDocument(null, DocumentIndexStatus.Indexed),                          // PRN222
            CreateDocument(null, DocumentIndexStatus.Indexed, subjectId: Guid.NewGuid()) // khác môn
        };

        var result = CalculateReport([], documents, [], [], [], []);

        Assert.Equal(1, result.IndexedCount);
        Assert.Equal(1, result.TotalDocuments);
    }

    // ─── Kiểm tra Tổng số Chunks ──────────────────────────────────────────────

    [Fact]
    public void Correctly_counts_total_chunks_for_prn222_documents()
    {
        var doc = CreateDocument(null, DocumentIndexStatus.Indexed);
        var otherDoc = CreateDocument(null, DocumentIndexStatus.Indexed, subjectId: Guid.NewGuid());

        var chunks = new List<DocumentChunk>
        {
            new DocumentChunk { Id = Guid.NewGuid(), DocumentId = doc.Id, ChunkIndex = 0, Content = "C0" },
            new DocumentChunk { Id = Guid.NewGuid(), DocumentId = doc.Id, ChunkIndex = 1, Content = "C1" },
            new DocumentChunk { Id = Guid.NewGuid(), DocumentId = doc.Id, ChunkIndex = 2, Content = "C2" },
            // Chunk của tài liệu môn khác — không được đếm
            new DocumentChunk { Id = Guid.NewGuid(), DocumentId = otherDoc.Id, ChunkIndex = 0, Content = "Other" }
        };

        var result = CalculateReport([], [doc, otherDoc], chunks, [], [], []);

        Assert.Equal(3, result.TotalChunks);
    }

    // ─── Kiểm tra Danh sách Tài liệu Lỗi ─────────────────────────────────────

    [Fact]
    public void RecentFailures_contains_failed_documents_with_error_messages()
    {
        var failedDoc = CreateDocument(null, DocumentIndexStatus.Failed);
        failedDoc.Title = "Bài giảng lỗi";
        failedDoc.IndexError = "Không thể parse file PDF";

        var indexedDoc = CreateDocument(null, DocumentIndexStatus.Indexed);

        var result = CalculateReport([], [failedDoc, indexedDoc], [], [], [], []);

        Assert.Single(result.RecentFailures);
        Assert.Equal("Bài giảng lỗi", result.RecentFailures[0].Title);
        Assert.Equal("Không thể parse file PDF", result.RecentFailures[0].IndexError);
    }

    [Fact]
    public void RecentFailures_does_not_include_non_failed_documents()
    {
        var documents = new List<Document>
        {
            CreateDocument(null, DocumentIndexStatus.Indexed),
            CreateDocument(null, DocumentIndexStatus.Uploaded),
            CreateDocument(null, DocumentIndexStatus.Processing)
        };

        var result = CalculateReport([], documents, [], [], [], []);

        Assert.Empty(result.RecentFailures);
    }

    [Fact]
    public void RecentFailures_is_capped_at_10_entries()
    {
        // Tạo 15 tài liệu lỗi — chỉ 10 cái đầu tiên (theo thứ tự mới nhất) được trả về
        var documents = Enumerable.Range(0, 15)
            .Select(i =>
            {
                var doc = CreateDocument(null, DocumentIndexStatus.Failed);
                doc.Title = $"Tài liệu lỗi #{i}";
                doc.IndexError = $"Lỗi số {i}";
                doc.UploadedAtUtc = DateTime.UtcNow.AddMinutes(-i); // mới nhất trước
                return doc;
            })
            .ToList();

        var result = CalculateReport([], documents, [], [], [], []);

        Assert.Equal(10, result.RecentFailures.Count);
        // Phần tử đầu tiên phải là tài liệu mới nhất (i=0)
        Assert.Equal("Tài liệu lỗi #0", result.RecentFailures[0].Title);
    }

    // ─── Kiểm tra Chat Usage (Zero State) ────────────────────────────────────

    [Fact]
    public void Chat_statistics_return_zero_when_no_flow2_data_exists()
    {
        var result = CalculateReport([], [], [], [], [], []);

        Assert.Equal(0, result.TotalChatSessions);
        Assert.Equal(0, result.TotalChatMessages);
        Assert.Equal(0, result.TotalMessageCitations);
    }

    [Fact]
    public void Chat_statistics_count_correctly_when_data_exists()
    {
        var sessions  = Enumerable.Range(0, 3).Select(_ => new ChatSession  { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Title = "Session", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow }).ToList();
        var messages  = Enumerable.Range(0, 7).Select(_ => new ChatMessage  { Id = Guid.NewGuid(), ChatSessionId = sessions[0].Id, Role = Domain.Enums.ChatMessageRole.User, Content = "msg", CreatedAtUtc = DateTime.UtcNow }).ToList();
        var citations = Enumerable.Range(0, 5).Select(_ => new MessageCitation { Id = Guid.NewGuid(), ChatMessageId = messages[0].Id, DocumentChunkId = Guid.NewGuid(), Rank = 1 }).ToList();

        var result = CalculateReport([], [], [], sessions, messages, citations);

        Assert.Equal(3, result.TotalChatSessions);
        Assert.Equal(7, result.TotalChatMessages);
        Assert.Equal(5, result.TotalMessageCitations);
    }

    // ─── Kiểm tra Tính Read-Only ─────────────────────────────────────────────

    [Fact]
    public void Report_calculation_does_not_modify_document_status()
    {
        var doc = CreateDocument(null, DocumentIndexStatus.Uploaded);
        var documents = new List<Document> { doc };
        var statusBefore = doc.IndexStatus;

        // Chạy tính toán báo cáo
        CalculateReport([], documents, [], [], [], []);

        // Xác nhận trạng thái document không thay đổi
        Assert.Equal(statusBefore, doc.IndexStatus);
    }

    [Fact]
    public void Report_calculation_does_not_modify_chapter_list()
    {
        var chapters = new List<Chapter>
        {
            new Chapter { Id = Guid.NewGuid(), SubjectId = SeedData.Prn222SubjectId, Number = 1, Title = "Chương 1" }
        };
        var countBefore = chapters.Count;

        CalculateReport(chapters, [], [], [], [], []);

        Assert.Equal(countBefore, chapters.Count);
    }

    // ─── Logic tính toán báo cáo (mirrors IndexModel.OnGetAsync logic) ───────

    /// <summary>
    /// Thực hiện toàn bộ logic báo cáo tương đương với <see cref="Pages.Reports.IndexModel.OnGetAsync"/>,
    /// áp dụng trên các danh sách in-memory để có thể test mà không cần kết nối DB.
    /// </summary>
    private static ReportResult CalculateReport(
        List<Chapter> allChapters,
        List<Document> allDocuments,
        List<DocumentChunk> allChunks,
        List<ChatSession> allSessions,
        List<ChatMessage> allMessages,
        List<MessageCitation> allCitations)
    {
        var prn222Chapters = allChapters
            .Where(c => c.SubjectId == SeedData.Prn222SubjectId)
            .OrderBy(c => c.Number)
            .ToList();

        var prn222Docs = allDocuments
            .Where(d => d.SubjectId == SeedData.Prn222SubjectId)
            .ToList();

        var chapterIds = prn222Chapters.Select(c => c.Id).ToHashSet();
        var prn222DocIds = prn222Docs.Select(d => d.Id).ToHashSet();

        var countMap = prn222Docs
            .Where(d => d.ChapterId.HasValue && chapterIds.Contains(d.ChapterId!.Value))
            .GroupBy(d => d.ChapterId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var documentsByChapter = prn222Chapters.Select(c => new ChapterCount
        {
            Number = c.Number,
            DocumentCount = countMap.TryGetValue(c.Id, out var cnt) ? cnt : 0
        }).ToList();

        var statusGroups = prn222Docs
            .GroupBy(d => d.IndexStatus)
            .ToDictionary(g => g.Key, g => g.Count());

        statusGroups.TryGetValue(DocumentIndexStatus.Uploaded,   out int uploadedCount);
        statusGroups.TryGetValue(DocumentIndexStatus.Processing, out int processingCount);
        statusGroups.TryGetValue(DocumentIndexStatus.Indexed,    out int indexedCount);
        statusGroups.TryGetValue(DocumentIndexStatus.Failed,     out int failedCount);

        var totalChunks = allChunks.Count(c => prn222DocIds.Contains(c.DocumentId));

        var recentFailures = prn222Docs
            .Where(d => d.IndexStatus == DocumentIndexStatus.Failed)
            .OrderByDescending(d => d.UploadedAtUtc)
            .Take(10)
            .Select(d => new FailureEntry { Title = d.Title, IndexError = d.IndexError })
            .ToList();

        return new ReportResult
        {
            TotalChapters       = prn222Chapters.Count,
            TotalDocuments      = prn222Docs.Count,
            UnassignedDocuments = prn222Docs.Count(d => d.ChapterId == null),
            TotalChunks         = totalChunks,
            UploadedCount       = uploadedCount,
            ProcessingCount     = processingCount,
            IndexedCount        = indexedCount,
            FailedCount         = failedCount,
            TotalChatSessions    = allSessions.Count,
            TotalChatMessages    = allMessages.Count,
            TotalMessageCitations = allCitations.Count,
            DocumentsByChapter  = documentsByChapter,
            RecentFailures      = recentFailures
        };
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static Document CreateDocument(
        Guid? chapterId,
        DocumentIndexStatus status,
        Guid? subjectId = null)
    {
        return new Document
        {
            Id               = Guid.NewGuid(),
            SubjectId        = subjectId ?? SeedData.Prn222SubjectId,
            ChapterId        = chapterId,
            UploadedByUserId = Guid.NewGuid(),
            Title            = "Tài liệu mẫu",
            OriginalFileName = "lecture.pdf",
            StoragePath      = "storage/uploads/lecture.pdf",
            ContentType      = "application/pdf",
            FileExtension    = ".pdf",
            FileSizeBytes    = 1024,
            IndexStatus      = status,
            UploadedAtUtc    = DateTime.UtcNow
        };
    }

    // ─── Internal Result Types ────────────────────────────────────────────────

    private sealed class ReportResult
    {
        public int TotalChapters        { get; init; }
        public int TotalDocuments       { get; init; }
        public int UnassignedDocuments  { get; init; }
        public int TotalChunks          { get; init; }
        public int UploadedCount        { get; init; }
        public int ProcessingCount      { get; init; }
        public int IndexedCount         { get; init; }
        public int FailedCount          { get; init; }
        public int TotalChatSessions    { get; init; }
        public int TotalChatMessages    { get; init; }
        public int TotalMessageCitations { get; init; }
        public List<ChapterCount>  DocumentsByChapter { get; init; } = [];
        public List<FailureEntry>  RecentFailures     { get; init; } = [];
    }

    private sealed class ChapterCount
    {
        public int Number        { get; init; }
        public int DocumentCount { get; init; }
    }

    private sealed class FailureEntry
    {
        public string  Title      { get; init; } = string.Empty;
        public string? IndexError { get; init; }
    }
}
