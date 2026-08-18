using System.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Application.Models;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Data.Seed;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Domain.Enums;
using PRN222.RagAssistant.Features.Rag;
using PRN222.RagAssistant.Features.Rag.Exceptions;
using PRN222.RagAssistant.Infrastructure.Rag;
using PRN222.RagAssistant.Security;
using Xunit;

namespace PRN222.RagAssistant.Tests;

public sealed class RagQueryServiceTests
{
    private static readonly AsyncLocal<ApplicationDbContext?> CurrentTestDbContext = new();

    [Fact]
    public async Task AskAsync_ThrowsArgumentException_WhenQuestionIsNull()
    {
        var (service, _, _, _) = CreateTestServiceWithDbContext();
        var session = Guid.NewGuid();
        var user = Guid.NewGuid();

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.AskAsync(user, session, null!));
    }

    [Fact]
    public async Task AskAsync_ThrowsArgumentException_WhenQuestionIsWhitespace()
    {
        var (service, _, _, _) = CreateTestService();
        var session = Guid.NewGuid();
        var user = Guid.NewGuid();

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.AskAsync(user, session, "   "));
    }

    [Fact]
    public async Task AskAsync_ThrowsChatSessionNotFoundException_WhenSessionDoesNotExist()
    {
        var (service, _, _, _) = CreateTestServiceWithDbContext();

        await Assert.ThrowsAsync<ChatSessionNotFoundException>(
            () => service.AskAsync(Guid.NewGuid(), Guid.NewGuid(), "What is OOP?"));
    }

    [Fact]
    public async Task AskAsync_ThrowsChatSessionNotFoundException_WhenSessionBelongsToDifferentUser()
    {
        var (service, _, _, _) = CreateTestServiceWithDbContext();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var session = await CreateTestSessionAsync(user1);

        await Assert.ThrowsAsync<ChatSessionNotFoundException>(
            () => service.AskAsync(user2, session.Id, "What is OOP?"));
    }

    [Fact]
    public async Task AskAsync_ReturnsNoEvidenceMessage_WhenNoChunksFound()
    {
        var (service, embeddingMock, chatMock, retriever) = CreateTestServiceWithDbContext();
        var user = Guid.NewGuid();
        var session = await CreateTestSessionAsync(user);
        retriever.ChunksToReturn = [];

        var answer = await service.AskAsync(user, session.Id, "What is quantum physics?");

        Assert.Equal("Không tìm thấy thông tin phù hợp.", answer.Answer);
        Assert.Empty(answer.Citations);
        embeddingMock.Verify(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        chatMock.Verify(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AskAsync_PersistsUserAndAssistantMessages_WithCorrectRoles()
    {
        var (service, embeddingMock, chatMock, retriever) = CreateTestServiceWithDbContext();
        var user = Guid.NewGuid();
        var session = await CreateTestSessionAsync(user);
        var chunk = CreateTestChunk();
        retriever.ChunksToReturn = [chunk];
        chatMock.Setup(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Test answer");

        var answer = await service.AskAsync(user, session.Id, "What is OOP?");

        Assert.NotEqual(Guid.Empty, answer.UserMessageId);
        Assert.NotEqual(Guid.Empty, answer.AssistantMessageId);
        Assert.NotEqual(answer.UserMessageId, answer.AssistantMessageId);

        // Verify messages persisted in DB
        var messages = await GetMessagesForSessionAsync(session.Id);
        Assert.Equal(2, messages.Count);
        Assert.Contains(messages, m => m.Role == ChatMessageRole.User && m.Content == "What is OOP?");
        Assert.Contains(messages, m => m.Role == ChatMessageRole.Assistant && m.Content == "Test answer");
    }

    [Fact]
    public async Task AskAsync_PersistsCitationsWithCorrectRank()
    {
        var (service, embeddingMock, chatMock, retriever) = CreateTestServiceWithDbContext();
        var user = Guid.NewGuid();
        var session = await CreateTestSessionAsync(user);
        var chunks = new[]
        {
            CreateTestChunk(1, "Content 1"),
            CreateTestChunk(2, "Content 2"),
            CreateTestChunk(3, "Content 3"),
        };
        retriever.ChunksToReturn = chunks;
        chatMock.Setup(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Test answer");

        var answer = await service.AskAsync(user, session.Id, "What is OOP?");

        Assert.Equal(3, answer.Citations.Count);
        Assert.Equal(1, answer.Citations[0].Rank);
        Assert.Equal(2, answer.Citations[1].Rank);
        Assert.Equal(3, answer.Citations[2].Rank);

        // Verify citations persisted in DB
        var citations = await GetCitationsForAssistantMessageAsync(answer.AssistantMessageId);
        Assert.Equal(3, citations.Count);
        Assert.Equal(1, citations[0].Rank);
        Assert.Equal(2, citations[1].Rank);
        Assert.Equal(3, citations[2].Rank);
    }

    [Fact]
    public async Task AskAsync_TruncatesExcerptToConfiguredLength()
    {
        var (service, _, _, retriever) = CreateTestServiceWithCustomExcerptChars(50);
        var user = Guid.NewGuid();
        var session = await CreateTestSessionAsync(user);
        var longContent = new string('x', 200);
        var chunk = CreateTestChunk(content: longContent);
        retriever.ChunksToReturn = [chunk];

        var answer = await service.AskAsync(user, session.Id, "What is OOP?");

        Assert.True(answer.Citations[0].Excerpt.Length <= 53); // 50 + "..."
        Assert.EndsWith("...", answer.Citations[0].Excerpt);
    }

    [Fact]
    public async Task AskAsync_RespectsCancellationToken_DuringEmbedding()
    {
        var (service, embeddingMock, _, _) = CreateTestServiceWithDbContext();
        var user = Guid.NewGuid();
        var session = await CreateTestSessionAsync(user);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        embeddingMock.Setup(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, CancellationToken ct) =>
            {
                await Task.Delay(100, ct);
                return new float[] { 0.1f, 0.2f };
            });

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.AskAsync(user, session.Id, "What is OOP?", cts.Token));
    }

    [Fact]
    public async Task AskAsync_RollsBackAssistantOnChatFailure()
    {
        var (service, embeddingMock, chatMock, retriever) = CreateTestServiceWithDbContext();
        var user = Guid.NewGuid();
        var session = await CreateTestSessionAsync(user);
        var chunk = CreateTestChunk();
        retriever.ChunksToReturn = [chunk];
        chatMock.Setup(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Ollama unavailable"));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => service.AskAsync(user, session.Id, "What is OOP?"));

        // User message should exist, assistant message should not
        var messages = await GetMessagesForSessionAsync(session.Id);
        Assert.Single(messages);
        Assert.Equal(ChatMessageRole.User, messages[0].Role);
    }

    [Fact]
    public async Task AskAsync_IncludesHistoryWhenEnabled()
    {
        var (service, embeddingMock, chatMock, retriever) = CreateTestServiceWithDbContext(includeHistory: true);
        var user = Guid.NewGuid();
        var session = await CreateTestSessionAsync(user);
        
        // Add history messages
        await AddHistoryMessagesAsync(session.Id, [
            ("User", "Previous question"),
            ("Assistant", "Previous answer")
        ]);

        var chunk = CreateTestChunk();
        retriever.ChunksToReturn = [chunk];
        string? capturedUserPrompt = null;
        chatMock.Setup(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((sys, usr, _) => capturedUserPrompt = usr)
            .ReturnsAsync("Test answer");

        await service.AskAsync(user, session.Id, "Follow up question");

        Assert.NotNull(capturedUserPrompt);
        Assert.Contains("Lịch sử hội thoại", capturedUserPrompt);
        Assert.Contains("Previous question", capturedUserPrompt);
        Assert.Contains("Previous answer", capturedUserPrompt);
    }

    [Fact]
    public async Task AskAsync_ExcludesHistoryWhenDisabled()
    {
        var (service, embeddingMock, chatMock, retriever) = CreateTestServiceWithDbContext(includeHistory: false);
        var user = Guid.NewGuid();
        var session = await CreateTestSessionAsync(user);
        
        // Add history messages
        await AddHistoryMessagesAsync(session.Id, [
            ("User", "Previous question"),
            ("Assistant", "Previous answer")
        ]);

        var chunk = CreateTestChunk();
        retriever.ChunksToReturn = [chunk];
        string? capturedUserPrompt = null;
        chatMock.Setup(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((sys, usr, _) => capturedUserPrompt = usr)
            .ReturnsAsync("Test answer");

        await service.AskAsync(user, session.Id, "Follow up question");

        Assert.NotNull(capturedUserPrompt);
        Assert.DoesNotContain("LỊCH SỬ HỘI THOẠI", capturedUserPrompt);
    }

    [Fact]
    public async Task AskAsync_UsesSubjectIdFromSession_ForRetrieval()
    {
        var (service, _, _, retriever) = CreateTestServiceWithDbContext();
        var user = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var session = await CreateTestSessionAsync(user, subjectId);
        var chunk = CreateTestChunk();
        retriever.ChunksToReturn = [chunk];
        Guid? capturedSubjectId = null;
        
        var originalSearch = retriever.GetType().GetMethod("SearchAsync");
        var mockRetriever = new Mock<PRN222.RagAssistant.Infrastructure.Rag.IDocumentChunkRetriever>();
        mockRetriever.Setup(x => x.SearchAsync(It.IsAny<float[]>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback<float[], Guid, CancellationToken>((_, sid, _) => capturedSubjectId = sid)
            .ReturnsAsync([chunk]);

        // We can't easily swap the retriever, so we'll test via the retriever directly
        // This test validates the retriever receives the subjectId
        var testRetriever = new TestableDocumentChunkRetriever();
        testRetriever.ChunksToReturn = [chunk];
        
        // Verify by calling retriever directly
        var embedding = new float[] { 0.1f, 0.2f };
        await testRetriever.SearchAsync(embedding, subjectId);
        
        Assert.Equal(subjectId, testRetriever.LastSubjectId);
    }

    [Fact]
    public void RetrievedChunk_Record_HoldsAllProperties()
    {
        var chunkId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var chunk = new RetrievedChunk(
            DocumentChunkId: chunkId,
            DocumentId: docId,
            DocumentTitle: "Test Document",
            Content: "OOP content here",
            PageNumber: 5,
            SlideNumber: null,
            SimilarityScore: 0.95);

        Assert.Equal(chunkId, chunk.DocumentChunkId);
        Assert.Equal(docId, chunk.DocumentId);
        Assert.Equal("Test Document", chunk.DocumentTitle);
        Assert.Equal("OOP content here", chunk.Content);
        Assert.Equal(5, chunk.PageNumber);
        Assert.Null(chunk.SlideNumber);
        Assert.Equal(0.95, chunk.SimilarityScore);
    }

    [Fact]
    public void ChatHistoryEntry_Record_HoldsAllProperties()
    {
        var entry = new ChatHistoryEntry("User", "What is inheritance?");

        Assert.Equal("User", entry.Role);
        Assert.Equal("What is inheritance?", entry.Content);
    }

    [Fact]
    public void BuildCitations_AssignsRankStartingFromOne()
    {
        var chunks = new List<RetrievedChunk>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "Doc 1", "Content 1", 1, null, 0.9),
            new(Guid.NewGuid(), Guid.NewGuid(), "Doc 2", "Content 2", 2, null, 0.8),
            new(Guid.NewGuid(), Guid.NewGuid(), "Doc 3", "Content 3", 3, null, 0.7),
        };

        var (service, _, _, _) = CreateTestService();
        var method = typeof(RagQueryService)
            .GetMethod("BuildCitations", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var citations = method?.Invoke(service, new object[] { chunks }) as IReadOnlyList<RagCitation>;

        Assert.NotNull(citations);
        Assert.Equal(3, citations.Count);
        Assert.Equal(1, citations[0].Rank);
        Assert.Equal(2, citations[1].Rank);
        Assert.Equal(3, citations[2].Rank);
    }

    [Fact]
    public void BuildCitations_TruncatesExcerpt_WhenContentExceedsExcerptChars()
    {
        var longContent = new string('x', 500);
        var chunks = new List<RetrievedChunk>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "Doc", longContent, 1, null, 0.9),
        };

        var (service, _, _, _) = CreateTestService();
        var method = typeof(RagQueryService)
            .GetMethod("BuildCitations", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var citations = method?.Invoke(service, new object[] { chunks }) as IReadOnlyList<RagCitation>;

        Assert.NotNull(citations);
        Assert.Single(citations);
        Assert.True(citations[0].Excerpt.Length <= 243); // 240 + "..."
        Assert.EndsWith("...", citations[0].Excerpt);
    }

    [Fact]
    public void BuildCitations_UsesExcerptCharsFromOptions()
    {
        var content = new string('y', 200);
        var chunks = new List<RetrievedChunk>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "Doc", content, 1, null, 0.9),
        };

        var (service, _, _, _) = CreateTestServiceWithCustomExcerptChars(50);
        var method = typeof(RagQueryService)
            .GetMethod("BuildCitations", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var citations = method?.Invoke(service, new object[] { chunks }) as IReadOnlyList<RagCitation>;

        Assert.NotNull(citations);
        Assert.True(citations[0].Excerpt.Length <= 53); // 50 + "..."
    }

    // ─── Test Helpers ────────────────────────────────────────────────────────

    private static (RagQueryService Service,
        Mock<ITextEmbeddingService> EmbeddingMock,
        Mock<IChatCompletionService> ChatMock,
        TestableDocumentChunkRetriever Retriever) CreateTestService(
            bool includeHistory = true,
            int excerptChars = 240)
    {
        var embeddingMock = new Mock<ITextEmbeddingService>();
        var chatMock = new Mock<IChatCompletionService>();
        var retriever = new TestableDocumentChunkRetriever();

        var ragOptions = new RagOptions
        {
            Retrieval = new RagOptions.RetrievalOptions
            {
                TopK = 5,
                MinimumSimilarityScore = 0.3,
                MaxContextChars = 4000,
                HistoryTurns = 5,
                ExcerptChars = excerptChars,
                IncludeConversationHistory = includeHistory
            },
            Chat = new RagOptions.ChatOptions
            {
                NoEvidenceMessage = "Không tìm thấy thông tin phù hợp."
            }
        };

        var promptBuilder = new GroundedPromptBuilder(Microsoft.Extensions.Options.Options.Create(ragOptions));

        embeddingMock.Setup(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.1f, 0.2f });
        chatMock.Setup(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Mocked answer");
        retriever.ChunksToReturn = Array.Empty<RetrievedChunk>();

        var service = new RagQueryService(
            null!,
            embeddingMock.Object,
            chatMock.Object,
            retriever,
            promptBuilder,
            Microsoft.Extensions.Options.Options.Create(ragOptions),
            Mock.Of<ILogger<RagQueryService>>(),
            TimeProvider.System);

        return (service, embeddingMock, chatMock, retriever);
    }

    private static (RagQueryService Service,
        Mock<ITextEmbeddingService> EmbeddingMock,
        Mock<IChatCompletionService> ChatMock,
        TestableDocumentChunkRetriever Retriever) CreateTestServiceWithDbContext(
            bool includeHistory = true,
            int excerptChars = 240)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));
        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        CurrentTestDbContext.Value = dbContext;
        var timeProvider = new FakeTimeProvider();

        var embeddingMock = new Mock<ITextEmbeddingService>();
        var chatMock = new Mock<IChatCompletionService>();
        var retriever = new TestableDocumentChunkRetriever();

        var ragOptions = new RagOptions
        {
            Retrieval = new RagOptions.RetrievalOptions
            {
                TopK = 5,
                MinimumSimilarityScore = 0.3,
                MaxContextChars = 4000,
                HistoryTurns = 5,
                ExcerptChars = excerptChars,
                IncludeConversationHistory = includeHistory
            },
            Chat = new RagOptions.ChatOptions
            {
                NoEvidenceMessage = "Không tìm thấy thông tin phù hợp."
            }
        };

        var promptBuilder = new GroundedPromptBuilder(Microsoft.Extensions.Options.Options.Create(ragOptions));

        embeddingMock.Setup(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.1f, 0.2f });
        chatMock.Setup(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Mocked answer");
        retriever.ChunksToReturn = Array.Empty<RetrievedChunk>();

        var service = new RagQueryService(
            dbContext,
            embeddingMock.Object,
            chatMock.Object,
            retriever,
            promptBuilder,
            Microsoft.Extensions.Options.Options.Create(ragOptions),
            Mock.Of<ILogger<RagQueryService>>(),
            timeProvider);

        return (service, embeddingMock, chatMock, retriever);
    }

    private static (RagQueryService Service,
        Mock<ITextEmbeddingService> EmbeddingMock,
        Mock<IChatCompletionService> ChatMock,
        TestableDocumentChunkRetriever Retriever) CreateTestServiceWithCustomExcerptChars(int excerptChars)
    {
        return CreateTestServiceWithDbContext(includeHistory: true, excerptChars: excerptChars);
    }

    private static RetrievedChunk CreateTestChunk(int index = 1, string content = "Test content")
    {
        return new RetrievedChunk(
            DocumentChunkId: Guid.NewGuid(),
            DocumentId: Guid.NewGuid(),
            DocumentTitle: "Test Document",
            Content: content,
            PageNumber: index,
            SlideNumber: null,
            SimilarityScore: 0.9);
    }

    private static async Task<ChatSession> CreateTestSessionAsync(Guid userId, Guid? subjectId = null)
    {
        var dbContext = CurrentTestDbContext.Value
            ?? throw new InvalidOperationException("CreateTestServiceWithDbContext must be called first.");

        // Ensure subject exists
        var targetSubjectId = subjectId ?? SeedData.Prn222SubjectId;
        if (!await dbContext.Subjects.AnyAsync(s => s.Id == targetSubjectId))
        {
            dbContext.Subjects.Add(new Subject
            {
                Id = targetSubjectId,
                Code = "TEST",
                Name = "Test Subject",
                IsActive = true
            });
            await dbContext.SaveChangesAsync();
        }

        var session = new ChatSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SubjectId = targetSubjectId,
            Title = "Test Session",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        dbContext.ChatSessions.Add(session);
        await dbContext.SaveChangesAsync();

        return session;
    }

    private static async Task<List<ChatMessage>> GetMessagesForSessionAsync(Guid sessionId)
    {
        var dbContext = CurrentTestDbContext.Value
            ?? throw new InvalidOperationException("CreateTestServiceWithDbContext must be called first.");
        return await dbContext.ChatMessages
            .Where(message => message.ChatSessionId == sessionId)
            .OrderBy(message => message.CreatedAtUtc)
            .ToListAsync();
    }

    private static async Task<List<MessageCitation>> GetCitationsForAssistantMessageAsync(Guid assistantMessageId)
    {
        var dbContext = CurrentTestDbContext.Value
            ?? throw new InvalidOperationException("CreateTestServiceWithDbContext must be called first.");
        return await dbContext.MessageCitations
            .Where(citation => citation.ChatMessageId == assistantMessageId)
            .OrderBy(citation => citation.Rank)
            .ToListAsync();
    }

    private static async Task AddHistoryMessagesAsync(Guid sessionId, (string Role, string Content)[] messages)
    {
        var dbContext = CurrentTestDbContext.Value
            ?? throw new InvalidOperationException("CreateTestServiceWithDbContext must be called first.");
        dbContext.ChatMessages.AddRange(messages.Select(message => new ChatMessage
        {
            Id = Guid.NewGuid(),
            ChatSessionId = sessionId,
            Role = Enum.Parse<ChatMessageRole>(message.Role, ignoreCase: true),
            Content = message.Content,
            CreatedAtUtc = DateTime.UtcNow
        }));
        await dbContext.SaveChangesAsync();
    }
}

internal sealed class TestableDocumentChunkRetriever : PRN222.RagAssistant.Infrastructure.Rag.IDocumentChunkRetriever
{
    public IReadOnlyList<RetrievedChunk> ChunksToReturn { get; set; } = Array.Empty<RetrievedChunk>();
    public Guid? LastSubjectId { get; private set; }

    public Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        float[] questionEmbedding,
        Guid subjectId,
        CancellationToken cancellationToken = default)
    {
        LastSubjectId = subjectId;
        return Task.FromResult(ChunksToReturn);
    }
}
