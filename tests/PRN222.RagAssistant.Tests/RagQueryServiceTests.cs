using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Application.Models;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Domain.Enums;
using PRN222.RagAssistant.Infrastructure.Rag;
using PRN222.RagAssistant.Infrastructure.Rag.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace PRN222.RagAssistant.Tests;


public sealed class RagQueryServiceTests
{
    #region Record Property Tests

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

    [Theory]
    [InlineData(0.9, 0.3)]
    [InlineData(0.3, 0.3)]
    [InlineData(1.0, 0.3)]
    public void RetrievedChunk_SimilarityScore_IsSetCorrectly(double score, double threshold)
    {
        var chunk = new RetrievedChunk(
            Guid.NewGuid(), Guid.NewGuid(), "Doc", "Content", null, null, score);

        var meetsThreshold = chunk.SimilarityScore >= threshold;
        Assert.True(meetsThreshold);
    }

    [Theory]
    [InlineData(0.29, 0.3)]
    [InlineData(0.1, 0.3)]
    public void RetrievedChunk_SimilarityScore_BelowThreshold(double score, double threshold)
    {
        var chunk = new RetrievedChunk(
            Guid.NewGuid(), Guid.NewGuid(), "Doc", "Content", null, null, score);

        var meetsThreshold = chunk.SimilarityScore >= threshold;
        Assert.False(meetsThreshold);
    }

    [Fact]
    public void RetrievedChunk_Equality_WorksCorrectly()
    {
        var chunkId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var chunk1 = new RetrievedChunk(chunkId, docId, "Doc", "Content", 1, null, 0.9);
        var chunk2 = new RetrievedChunk(chunkId, docId, "Doc", "Content", 1, null, 0.9);

        Assert.Equal(chunk1, chunk2);
    }

    #endregion

    #region Service Tests

    [Fact]
    public async Task AskAsync_ThrowsArgumentException_WhenQuestionIsEmpty()
    {
        // Arrange
        var (service, _) = CreateServiceWithMocks();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => service.AskAsync(userId, sessionId, "   "));
    }

    [Fact]
    public async Task AskAsync_ThrowsChatSessionNotFoundException_WhenSessionDoesNotExist()
    {
        // Arrange
        var (service, dbContext) = CreateServiceWithMocks();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<ChatSessionNotFoundException>(
            () => service.AskAsync(userId, sessionId, "Test question"));
    }

    [Fact]
    public async Task AskAsync_ThrowsChatSessionNotFoundException_WhenSessionBelongsToDifferentUser()
    {
        // Arrange
        var (service, dbContext) = CreateServiceWithMocks();

        var otherUserId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        dbContext.ChatSessions.Add(new ChatSession
        {
            Id = sessionId,
            UserId = otherUserId,
            Title = "Other session"
        });
        await dbContext.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ChatSessionNotFoundException>(
            () => service.AskAsync(userId, sessionId, "Test question"));
    }

    [Fact]
    public async Task AskAsync_PersistsUserMessage_WhenSuccessful()
    {
        // Arrange
        var (service, dbContext) = CreateServiceWithMocks();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var question = "What is OOP?";

        await SetupSessionAsync(dbContext, userId, sessionId);

        // Act
        await service.AskAsync(userId, sessionId, question);

        // Assert
        var userMessage = await dbContext.ChatMessages
            .FirstOrDefaultAsync(m => m.ChatSessionId == sessionId && m.Role == ChatMessageRole.User);

        Assert.NotNull(userMessage);
        Assert.Equal(question, userMessage.Content);
    }

    [Fact]
    public async Task AskAsync_PersistsAssistantMessage_WhenSuccessful()
    {
        // Arrange
        var expectedAnswer = "Answer with citations [1].";
        var (service, dbContext) = CreateServiceWithMocks(answerWithCitations: expectedAnswer);
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        await SetupSessionAsync(dbContext, userId, sessionId);

        // Act
        await service.AskAsync(userId, sessionId, "What is OOP?");

        // Assert
        var assistantMessage = await dbContext.ChatMessages
            .FirstOrDefaultAsync(m => m.ChatSessionId == sessionId && m.Role == ChatMessageRole.Assistant);

        Assert.NotNull(assistantMessage);
        Assert.Equal(expectedAnswer, assistantMessage.Content);
    }

    [Fact]
    public async Task AskAsync_ReturnsNoEvidenceMessage_WhenNoChunksFound()
    {
        // Arrange
        var (service, dbContext) = CreateServiceWithMocks(noChunks: true);
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        await SetupSessionAsync(dbContext, userId, sessionId);

        // Act
        var result = await service.AskAsync(userId, sessionId, "What is OOP?");

        // Assert
        Assert.Equal("Không tìm thấy thông tin liên quan.", result.Answer);
        Assert.Empty(result.Citations);
    }

    [Fact]
    public async Task AskAsync_ParsesCitationsFromAnswer_WhenModelUsesMarkers()
    {
        // Arrange
        var (service, dbContext) = CreateServiceWithMocks(
            answerWithCitations: "OOP is Object-Oriented Programming [1].");
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        await SetupSessionAsync(dbContext, userId, sessionId);

        // Act
        var result = await service.AskAsync(userId, sessionId, "What is OOP?");

        // Assert
        Assert.Single(result.Citations);
        Assert.Equal(1, result.Citations[0].Rank);
    }

    [Fact]
    public async Task AskAsync_ReturnsEmptyCitations_WhenModelDoesNotUseMarkers()
    {
        // Arrange
        var (service, dbContext) = CreateServiceWithMocks(
            answerWithCitations: "OOP is a programming paradigm. No citations used.");
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        await SetupSessionAsync(dbContext, userId, sessionId);

        // Act
        var result = await service.AskAsync(userId, sessionId, "What is OOP?");

        // Assert
        Assert.Empty(result.Citations);
    }

    [Fact]
    public async Task AskAsync_DoesNotIncludeCurrentQuestion_InHistory()
    {
        // Arrange
        var (service, dbContext) = CreateServiceWithMocks();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var existingQuestion = "Previous question";
        var existingAnswer = "Previous answer";
        var currentQuestion = "Current question";

        await SetupSessionAsync(dbContext, userId, sessionId);

        // Add existing messages
        dbContext.ChatMessages.Add(new ChatMessage
        {
            Id = Guid.CreateVersion7(),
            ChatSessionId = sessionId,
            Role = ChatMessageRole.User,
            Content = existingQuestion,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
        });
        dbContext.ChatMessages.Add(new ChatMessage
        {
            Id = Guid.CreateVersion7(),
            ChatSessionId = sessionId,
            Role = ChatMessageRole.Assistant,
            Content = existingAnswer,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-4)
        });
        await dbContext.SaveChangesAsync();

        // Act
        await service.AskAsync(userId, sessionId, currentQuestion);

        // Assert - Current question should not be in history
        // History is loaded BEFORE persisting current question, so current question won't appear
        var allMessages = await dbContext.ChatMessages
            .Where(m => m.ChatSessionId == sessionId)
            .OrderBy(m => m.CreatedAtUtc)
            .ToListAsync();

        Assert.Equal(4, allMessages.Count); // 2 existing + 2 new (user + assistant)
        Assert.Equal(existingQuestion, allMessages[0].Content);
        Assert.Equal(existingAnswer, allMessages[1].Content);
        Assert.Equal(currentQuestion, allMessages[2].Content);
    }

    [Fact]
    public async Task AskAsync_UsesSessionSubjectId_ForRetrieval()
    {
        // Arrange
        var subjectId = Guid.NewGuid();
        var (service, dbContext, mockRetriever) = CreateServiceWithMocksAndRetriever();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        dbContext.ChatSessions.Add(new ChatSession
        {
            Id = sessionId,
            UserId = userId,
            SubjectId = subjectId,
            Title = "Existing session" // Pre-set title to skip ExecuteUpdate
        });
        await dbContext.SaveChangesAsync();

        // Act
        await service.AskAsync(userId, sessionId, "What is OOP?");

        // Assert - The retriever should have been called with subjectId
        mockRetriever.Verify(
            x => x.SearchAsync(It.IsAny<float[]>(), subjectId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AskAsync_UsesProvidedSubjectId_WhenSessionSubjectIdIsNull()
    {
        // Arrange
        var providedSubjectId = Guid.NewGuid();
        var (service, dbContext, mockRetriever) = CreateServiceWithMocksAndRetriever();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        await SetupSessionAsync(dbContext, userId, sessionId);

        // Act
        var result = await service.AskAsync(userId, sessionId, "What is OOP?", providedSubjectId);

        // Assert
        Assert.NotNull(result);
        mockRetriever.Verify(
            x => x.SearchAsync(It.IsAny<float[]>(), providedSubjectId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AskAsync_ThrowsArgumentException_WhenProvidedSubjectIdDiffersFromSessionSubjectId()
    {
        // Arrange
        var sessionSubjectId = Guid.NewGuid();
        var conflictingSubjectId = Guid.NewGuid();
        var (service, dbContext) = CreateServiceWithMocks();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        dbContext.ChatSessions.Add(new ChatSession
        {
            Id = sessionId,
            UserId = userId,
            SubjectId = sessionSubjectId,
            Title = "Existing session"
        });
        await dbContext.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.AskAsync(userId, sessionId, "What is OOP?", conflictingSubjectId));
    }

    [Fact]
    public async Task AskAsync_PersistsCitations_ToDatabase()
    {
        // Arrange
        var (service, dbContext) = CreateServiceWithMocks(
            answerWithCitations: "OOP is Object-Oriented Programming [1][2].");
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        await SetupSessionAsync(dbContext, userId, sessionId);

        // Act
        var result = await service.AskAsync(userId, sessionId, "What is OOP?");

        // Assert
        var messageId = result.AssistantMessageId;
        var citations = await dbContext.MessageCitations
            .Where(c => c.ChatMessageId == messageId)
            .ToListAsync();

        Assert.Equal(2, citations.Count);
    }

    [Fact]
    public async Task AskAsync_DoesNotPersistMessages_WhenEmbeddingServiceThrows()
    {
        // Arrange
        var dbContext = CreateInMemoryDbContext();
        var mockEmbeddingService = new Mock<ITextEmbeddingService>();
        var mockChatService = new Mock<IChatCompletionService>();
        var mockRetriever = new Mock<IDocumentChunkRetriever>();
        var options = Options.Create(new RagOptions());

        mockEmbeddingService
            .Setup(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Ollama unavailable"));

        var service = new RagQueryService(
            dbContext,
            mockEmbeddingService.Object,
            mockChatService.Object,
            mockRetriever.Object,
            new GroundedPromptBuilder(options),
            options,
            NullLogger<RagQueryService>.Instance,
            TimeProvider.System);

        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        await SetupSessionAsync(dbContext, userId, sessionId);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.AskAsync(userId, sessionId, "What is OOP?"));

        var messageCount = await dbContext.ChatMessages.CountAsync();
        Assert.Equal(0, messageCount);
    }

    [Fact]
    public async Task AskAsync_DoesNotPersistMessages_WhenChatCompletionServiceThrows()
    {
        // Arrange
        var dbContext = CreateInMemoryDbContext();
        var mockEmbeddingService = new Mock<ITextEmbeddingService>();
        var mockChatService = new Mock<IChatCompletionService>();
        var mockRetriever = new Mock<IDocumentChunkRetriever>();
        var options = Options.Create(new RagOptions());

        mockEmbeddingService
            .Setup(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[1024]);

        mockRetriever
            .Setup(x => x.SearchAsync(It.IsAny<float[]>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new RetrievedChunk(Guid.NewGuid(), Guid.NewGuid(), "Doc", "Content", 1, null, 0.9)
            });

        mockChatService
            .Setup(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("API rate limit"));

        var service = new RagQueryService(
            dbContext,
            mockEmbeddingService.Object,
            mockChatService.Object,
            mockRetriever.Object,
            new GroundedPromptBuilder(options),
            options,
            NullLogger<RagQueryService>.Instance,
            TimeProvider.System);

        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        await SetupSessionAsync(dbContext, userId, sessionId);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AskAsync(userId, sessionId, "What is OOP?"));

        var messageCount = await dbContext.ChatMessages.CountAsync();
        Assert.Equal(0, messageCount);
    }

    [Fact]
    public async Task GetOrCreateUserSessionAsync_CreatesNewSession_WithActiveSubject()
    {
        // Arrange
        var (service, dbContext) = CreateServiceWithMocks();
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        dbContext.Subjects.Add(new Subject
        {
            Id = subjectId,
            Code = "PRN222",
            Name = "C# Programming",
            IsActive = true
        });
        await dbContext.SaveChangesAsync();

        // Act
        var sessionId = await service.GetOrCreateUserSessionAsync(userId);

        // Assert
        var session = await dbContext.ChatSessions.FindAsync(sessionId);
        Assert.NotNull(session);
        Assert.Equal(userId, session.UserId);
        Assert.Equal(subjectId, session.SubjectId);
    }

    [Fact]
    public async Task GetOrCreateUserSessionAsync_ReturnsExistingSession_WhenOneAlreadyExists()
    {
        // Arrange
        var (service, dbContext) = CreateServiceWithMocks();
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var existingSessionId = Guid.NewGuid();

        dbContext.ChatSessions.Add(new ChatSession
        {
            Id = existingSessionId,
            UserId = userId,
            SubjectId = subjectId,
            Title = "Existing"
        });
        await dbContext.SaveChangesAsync();

        // Act
        var sessionId = await service.GetOrCreateUserSessionAsync(userId, subjectId);

        // Assert
        Assert.Equal(existingSessionId, sessionId);
    }

    #endregion



    #region Helper Methods

    private static (RagQueryService service, ApplicationDbContext dbContext) CreateServiceWithMocks(
        bool noChunks = false,
        string answerWithCitations = "Answer with citations [1].")
    {
        var (service, dbContext, _) = CreateServiceWithMocksAndRetriever(noChunks, answerWithCitations);
        return (service, dbContext);
    }

    private static (RagQueryService service, ApplicationDbContext dbContext, Mock<IDocumentChunkRetriever> mockRetriever) CreateServiceWithMocksAndRetriever(
        bool noChunks = false,
        string answerWithCitations = "Answer with citations [1].")
    {
        var dbContext = CreateInMemoryDbContext();
        var mockEmbeddingService = new Mock<ITextEmbeddingService>();
        var mockChatService = new Mock<IChatCompletionService>();
        var mockRetriever = new Mock<IDocumentChunkRetriever>();
        var options = Options.Create(new RagOptions
        {
            Retrieval = new RagOptions.RetrievalOptions
            {
                TopK = 5,
                MinimumSimilarityScore = 0.3,
                HistoryTurns = 5,
                ExcerptChars = 200,
                IncludeConversationHistory = true
            },
            Chat = new RagOptions.ChatOptions
            {
                NoEvidenceMessage = "Không tìm thấy thông tin liên quan."
            }
        });

        mockEmbeddingService
            .Setup(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[1024]);

        var chunks = noChunks
            ? Array.Empty<RetrievedChunk>()
            : new[]
            {
                new RetrievedChunk(
                    DocumentChunkId: Guid.NewGuid(),
                    DocumentId: Guid.NewGuid(),
                    DocumentTitle: "OOP Guide",
                    Content: "OOP content",
                    PageNumber: 1,
                    SlideNumber: null,
                    SimilarityScore: 0.95),
                new RetrievedChunk(
                    DocumentChunkId: Guid.NewGuid(),
                    DocumentId: Guid.NewGuid(),
                    DocumentTitle: "Programming Basics",
                    Content: "Programming basics content",
                    PageNumber: 2,
                    SlideNumber: null,
                    SimilarityScore: 0.85)
            };

        mockRetriever
            .Setup(x => x.SearchAsync(It.IsAny<float[]>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(chunks);

        mockChatService
            .Setup(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(answerWithCitations);

        var service = new RagQueryService(
            dbContext,
            mockEmbeddingService.Object,
            mockChatService.Object,
            mockRetriever.Object,
            new GroundedPromptBuilder(options),
            options,
            NullLogger<RagQueryService>.Instance,
            TimeProvider.System);

        return (service, dbContext, mockRetriever);
    }


    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        // Use PostgreSQL model to support Vector type in production schema
        var postgresOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(
                "Host=unused;Database=unused;Username=unused;Password=unused",
                npgsql => npgsql.UseVector())
            .Options;
        using var postgresContext = new ApplicationDbContext(postgresOptions);

        var inMemoryOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"rag-test-{Guid.NewGuid()}")
            .UseModel(postgresContext.Model)
            .Options;

        return new ApplicationDbContext(inMemoryOptions);
    }

    private static async Task SetupSessionAsync(ApplicationDbContext dbContext, Guid userId, Guid sessionId)
    {
        dbContext.ChatSessions.Add(new ChatSession
        {
            Id = sessionId,
            UserId = userId,
            Title = "Existing session" // Pre-set title to skip ExecuteUpdate in EnsureSessionTitleAsync
        });
        await dbContext.SaveChangesAsync();
    }

    #endregion
}
