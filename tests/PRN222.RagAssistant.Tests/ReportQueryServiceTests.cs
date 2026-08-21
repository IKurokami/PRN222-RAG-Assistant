using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Domain.Enums;
using PRN222.RagAssistant.Infrastructure.Services;
using PRN222.RagAssistant.Pages.Reports;

namespace PRN222.RagAssistant.Tests;

public sealed class ReportQueryServiceTests
{
    [Fact]
    public void Reports_page_depends_on_report_query_service_instead_of_db_context()
    {
        var constructor = Assert.Single(typeof(IndexModel).GetConstructors());
        var parameterTypes = constructor.GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToList();

        Assert.Contains(typeof(IReportQueryService), parameterTypes);
        Assert.DoesNotContain(typeof(ApplicationDbContext), parameterTypes);
    }

    [Fact]
    public async Task GetSubjectReportAsync_returns_null_for_unknown_subject()
    {
        await using var dbContext = CreateContext();
        var service = new ReportQueryService(dbContext);

        var result = await service.GetSubjectReportAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetSubjectReportAsync_scopes_document_chat_and_citation_insights_to_subject()
    {
        await using var dbContext = CreateContext();

        var targetSubjectId = Guid.NewGuid();
        var otherSubjectId = Guid.NewGuid();
        var targetChapterId = Guid.NewGuid();
        var otherChapterId = Guid.NewGuid();
        var targetIndexedDocumentId = Guid.NewGuid();
        var targetUncitedDocumentId = Guid.NewGuid();
        var targetFailedDocumentId = Guid.NewGuid();
        var otherDocumentId = Guid.NewGuid();
        var targetChunkId = Guid.NewGuid();
        var targetSecondChunkId = Guid.NewGuid();
        var targetUncitedChunkId = Guid.NewGuid();
        var otherChunkId = Guid.NewGuid();
        var targetSessionId = Guid.NewGuid();
        var otherSessionId = Guid.NewGuid();
        var legacySessionId = Guid.NewGuid();
        var targetUserMessageId = Guid.NewGuid();
        var targetAssistantMessageId = Guid.NewGuid();
        var targetUncitedAssistantMessageId = Guid.NewGuid();
        var otherMessageId = Guid.NewGuid();
        var legacyMessageId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        dbContext.Subjects.AddRange(
            new Subject
            {
                Id = targetSubjectId,
                Code = "TARGET",
                Name = "Target subject",
                IsActive = true
            },
            new Subject
            {
                Id = otherSubjectId,
                Code = "OTHER",
                Name = "Other subject",
                IsActive = true
            });

        dbContext.Chapters.AddRange(
            new Chapter
            {
                Id = targetChapterId,
                SubjectId = targetSubjectId,
                Number = 1,
                Title = "Target chapter"
            },
            new Chapter
            {
                Id = otherChapterId,
                SubjectId = otherSubjectId,
                Number = 1,
                Title = "Other chapter"
            });

        dbContext.Documents.AddRange(
            CreateDocument(
                targetIndexedDocumentId,
                targetSubjectId,
                targetChapterId,
                DocumentIndexStatus.Indexed,
                now.AddMinutes(-5)),
            CreateDocument(
                targetUncitedDocumentId,
                targetSubjectId,
                targetChapterId,
                DocumentIndexStatus.Indexed,
                now.AddMinutes(-10)),
            CreateDocument(
                targetFailedDocumentId,
                targetSubjectId,
                null,
                DocumentIndexStatus.Failed,
                null,
                "Index failed"),
            CreateDocument(
                otherDocumentId,
                otherSubjectId,
                otherChapterId,
                DocumentIndexStatus.Indexed,
                now.AddMinutes(-1)));

        dbContext.DocumentChunks.AddRange(
            new DocumentChunk
            {
                Id = targetChunkId,
                DocumentId = targetIndexedDocumentId,
                ChunkIndex = 0,
                Content = "target chunk 1"
            },
            new DocumentChunk
            {
                Id = targetSecondChunkId,
                DocumentId = targetIndexedDocumentId,
                ChunkIndex = 1,
                Content = "target chunk 2"
            },
            new DocumentChunk
            {
                Id = targetUncitedChunkId,
                DocumentId = targetUncitedDocumentId,
                ChunkIndex = 0,
                Content = "target uncited chunk"
            },
            new DocumentChunk
            {
                Id = otherChunkId,
                DocumentId = otherDocumentId,
                ChunkIndex = 0,
                Content = "other chunk"
            });

        dbContext.ChatSessions.AddRange(
            CreateSession(targetSessionId, targetSubjectId, now),
            CreateSession(otherSessionId, otherSubjectId, now),
            CreateSession(legacySessionId, null, now));

        dbContext.ChatMessages.AddRange(
            CreateMessage(targetUserMessageId, targetSessionId, ChatMessageRole.User, now),
            CreateMessage(targetAssistantMessageId, targetSessionId, ChatMessageRole.Assistant, now),
            CreateMessage(targetUncitedAssistantMessageId, targetSessionId, ChatMessageRole.Assistant, now),
            CreateMessage(otherMessageId, otherSessionId, ChatMessageRole.Assistant, now),
            CreateMessage(legacyMessageId, legacySessionId, ChatMessageRole.Assistant, now));

        dbContext.MessageCitations.AddRange(
            new MessageCitation
            {
                Id = Guid.NewGuid(),
                ChatMessageId = targetAssistantMessageId,
                DocumentChunkId = targetChunkId,
                Rank = 1
            },
            new MessageCitation
            {
                Id = Guid.NewGuid(),
                ChatMessageId = otherMessageId,
                DocumentChunkId = otherChunkId,
                Rank = 1
            },
            new MessageCitation
            {
                Id = Guid.NewGuid(),
                ChatMessageId = legacyMessageId,
                DocumentChunkId = otherChunkId,
                Rank = 1
            });

        await dbContext.SaveChangesAsync();

        var service = new ReportQueryService(dbContext);
        var result = await service.GetSubjectReportAsync(targetSubjectId);

        Assert.NotNull(result);
        Assert.Equal(targetSubjectId, result!.SubjectId);
        Assert.Equal("TARGET", result.SubjectCode);
        Assert.Equal(1, result.TotalChapters);
        Assert.Equal(3, result.TotalDocuments);
        Assert.Equal(1, result.UnassignedDocuments);
        Assert.Equal(3, result.TotalChunks);
        Assert.Equal(0, result.UploadedCount);
        Assert.Equal(0, result.ProcessingCount);
        Assert.Equal(2, result.IndexedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(1.5, result.AverageChunksPerIndexedDocument);

        var chapter = Assert.Single(result.DocumentsByChapter);
        Assert.Equal(targetChapterId, chapter.Id);
        Assert.Equal(2, chapter.DocumentCount);

        var failure = Assert.Single(result.RecentFailures);
        Assert.Equal(targetFailedDocumentId, failure.DocumentId);
        Assert.Equal("Index failed", failure.IndexError);

        Assert.Equal(2, result.RecentlyIndexed.Count);
        Assert.Contains(result.RecentlyIndexed, item =>
            item.DocumentId == targetIndexedDocumentId && item.ChunkCount == 2);
        Assert.Contains(result.RecentlyIndexed, item =>
            item.DocumentId == targetUncitedDocumentId && item.ChunkCount == 1);

        Assert.Equal(1, result.TotalChatSessions);
        Assert.Equal(3, result.TotalChatMessages);
        Assert.Equal(1, result.TotalMessageCitations);
        Assert.Equal(1, result.UserQuestionCount);
        Assert.Equal(2, result.AssistantResponseCount);
        Assert.Equal(1, result.CitedAssistantResponseCount);
        Assert.Equal(1, result.ActiveSessionsLast7Days);
        Assert.Equal(1, result.ActiveSessionsLast30Days);
        Assert.Equal(3, result.AverageMessagesPerSession);
        Assert.Equal(0.5, result.AverageCitationsPerAssistantResponse);
        Assert.Equal(50, result.CitationCoveragePercent);

        Assert.Equal(1, result.UniqueCitedDocuments);
        Assert.Equal(1, result.IndexedButNeverCitedDocuments);
        Assert.Equal(50, result.CitedDocumentCoveragePercent);
        Assert.Equal(100, result.TopThreeCitationSharePercent);

        var topDocument = Assert.Single(result.TopCitedDocuments);
        Assert.Equal(targetIndexedDocumentId, topDocument.DocumentId);
        Assert.Equal(1, topDocument.CitationCount);
        Assert.Equal(1, topDocument.DistinctSessions);
        Assert.Equal(1, topDocument.CitedChunkCount);
        Assert.Equal(1, topDocument.ChapterNumber);

        var topChapter = Assert.Single(result.TopCitedChapters);
        Assert.Equal(targetChapterId, topChapter.ChapterId);
        Assert.Equal(2, topChapter.DocumentCount);
        Assert.Equal(1, topChapter.CitedDocumentCount);
        Assert.Equal(1, topChapter.CitationCount);

        var today = Assert.Single(result.DailyActivityLast7Days, item => item.DateUtc.Date == now.Date);
        Assert.Equal(1, today.UserMessages);
        Assert.Equal(2, today.AssistantMessages);
        Assert.Equal(1, today.CitationCount);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"report-tests-{Guid.NewGuid()}")
            .ReplaceService<IModelCustomizer, ReportTestModelCustomizer>()
            .Options;

        return new ApplicationDbContext(options);
    }

    private static Document CreateDocument(
        Guid id,
        Guid subjectId,
        Guid? chapterId,
        DocumentIndexStatus status,
        DateTime? indexedAtUtc,
        string? indexError = null)
    {
        return new Document
        {
            Id = id,
            SubjectId = subjectId,
            ChapterId = chapterId,
            UploadedByUserId = Guid.NewGuid(),
            Title = $"Document {id}",
            OriginalFileName = $"{id}.pdf",
            StoragePath = $"storage/uploads/{id}.pdf",
            ContentType = "application/pdf",
            FileExtension = ".pdf",
            FileSizeBytes = 1024,
            IndexStatus = status,
            IndexError = indexError,
            UploadedAtUtc = DateTime.UtcNow.AddHours(-1),
            IndexedAtUtc = indexedAtUtc
        };
    }

    private static ChatSession CreateSession(Guid id, Guid? subjectId, DateTime now)
    {
        return new ChatSession
        {
            Id = id,
            UserId = Guid.NewGuid(),
            SubjectId = subjectId,
            Title = "Session",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    private static ChatMessage CreateMessage(
        Guid id,
        Guid sessionId,
        ChatMessageRole role,
        DateTime now)
    {
        return new ChatMessage
        {
            Id = id,
            ChatSessionId = sessionId,
            Role = role,
            Content = "message",
            CreatedAtUtc = now
        };
    }

    private sealed class ReportTestModelCustomizer : ModelCustomizer
    {
        public ReportTestModelCustomizer(ModelCustomizerDependencies dependencies)
            : base(dependencies)
        {
        }

        public override void Customize(ModelBuilder modelBuilder, DbContext context)
        {
            base.Customize(modelBuilder, context);
            modelBuilder.Entity<DocumentChunk>().Ignore(chunk => chunk.Embedding);
        }
    }
}
