using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Pgvector.EntityFrameworkCore;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Infrastructure.Rag;
using PRN222.RagAssistant.Infrastructure.Services;
using Xunit;

namespace PRN222.RagAssistant.Tests;

public sealed class AgenticRagRegressionTests
{
    [Fact]
    public async Task AskAsync_AllowsMetadataOnlyAnswer_AfterListDocumentsTool()
    {
        var subjectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var dbContext = CreateInMemoryDbContext();
        await SetupSessionAsync(dbContext, userId, sessionId, subjectId);

        var retrieval = new Mock<IAgenticRetrievalService>();
        retrieval
            .Setup(x => x.ListDocumentsAsync(
                subjectId,
                It.IsAny<string?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new AgentDocumentInfo(Guid.NewGuid(), "OOP Guide", "oop-guide.pdf", DateTime.UtcNow)
            ]);

        var agent = new ToolCallingChatService(
            "list_documents",
            string.Empty,
            "Có tài liệu OOP Guide trong môn học hiện tại.");
        var service = CreateService(dbContext, agent, retrieval.Object);

        var result = await service.AskAsync(
            userId,
            sessionId,
            "Có những tài liệu nào?",
            subjectId);

        Assert.Equal("Có tài liệu OOP Guide trong môn học hiện tại.", result.Answer);
        Assert.Empty(result.Citations);
        Assert.Contains("OOP Guide", agent.ToolResult);
    }

    [Fact]
    public async Task AskAsync_RejectsChunkBasedAnswer_WithoutValidCitationMarker()
    {
        var subjectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var dbContext = CreateInMemoryDbContext();
        await SetupSessionAsync(dbContext, userId, sessionId, subjectId);

        var chunk = new RetrievedChunk(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "OOP Guide",
            "Encapsulation hides internal implementation details.",
            4,
            null,
            0.91);

        var retrieval = new Mock<IAgenticRetrievalService>();
        retrieval
            .Setup(x => x.HybridSearchAsync(
                It.IsAny<string>(),
                subjectId,
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([chunk]);

        var agent = new ToolCallingChatService(
            "search_documents",
            "encapsulation",
            "Encapsulation hides implementation details.");
        var service = CreateService(dbContext, agent, retrieval.Object);

        var result = await service.AskAsync(
            userId,
            sessionId,
            "Encapsulation là gì?",
            subjectId);

        Assert.Equal("Không tìm thấy thông tin liên quan.", result.Answer);
        Assert.Empty(result.Citations);

        var persisted = await dbContext.ChatMessages
            .SingleAsync(message => message.Id == result.AssistantMessageId);
        Assert.Equal("Không tìm thấy thông tin liên quan.", persisted.Content);
    }

    [Fact]
    public async Task AskAsync_PreservesChunkBasedAnswer_WithValidCitationMarker()
    {
        var subjectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var dbContext = CreateInMemoryDbContext();
        await SetupSessionAsync(dbContext, userId, sessionId, subjectId);

        var chunk = new RetrievedChunk(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "OOP Guide",
            "Encapsulation hides internal implementation details.",
            4,
            null,
            0.91);

        var retrieval = new Mock<IAgenticRetrievalService>();
        retrieval
            .Setup(x => x.HybridSearchAsync(
                It.IsAny<string>(),
                subjectId,
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([chunk]);

        var agent = new ToolCallingChatService(
            "search_documents",
            "encapsulation",
            "Encapsulation che giấu chi tiết triển khai [1].");
        var service = CreateService(dbContext, agent, retrieval.Object);

        var result = await service.AskAsync(
            userId,
            sessionId,
            "Encapsulation là gì?",
            subjectId);

        Assert.Equal("Encapsulation che giấu chi tiết triển khai [1].", result.Answer);
        var citation = Assert.Single(result.Citations);
        Assert.Equal(chunk.DocumentChunkId, citation.DocumentChunkId);
        Assert.Equal(1, citation.Rank);
    }

    [Fact]
    public void AgenticRetrievalRanker_DropsSemanticCandidates_BelowConfiguredThreshold()
    {
        var weakChunk = new RetrievedChunk(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Unrelated",
            "Weak semantic neighbor",
            1,
            null,
            0.12);

        var results = AgenticRetrievalRanker.Fuse(
            [weakChunk],
            Array.Empty<RetrievedChunk>(),
            minimumSemanticSimilarity: 0.3,
            topK: 6);

        Assert.Empty(results);
    }

    [Fact]
    public void AgenticRetrievalRanker_KeepsKeywordMatch_WhenSemanticScoreIsWeak()
    {
        var chunkId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var weakSemantic = new RetrievedChunk(
            chunkId,
            documentId,
            "Version Notes",
            "PRN222 version 2026",
            1,
            null,
            0.12);
        var keywordMatch = weakSemantic with { SimilarityScore = 0.8 };

        var results = AgenticRetrievalRanker.Fuse(
            [weakSemantic],
            [keywordMatch],
            minimumSemanticSimilarity: 0.3,
            topK: 6);

        var result = Assert.Single(results);
        Assert.Equal(chunkId, result.DocumentChunkId);
    }

    private static RagQueryService CreateService(
        ApplicationDbContext dbContext,
        IChatCompletionService chatService,
        IAgenticRetrievalService retrieval)
    {
        var embedding = new Mock<ITextEmbeddingService>();
        embedding
            .Setup(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[1024]);

        var vectorRetriever = new Mock<IDocumentChunkRetriever>();
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
            },
            Agentic = new RagOptions.AgenticOptions
            {
                Enabled = true,
                ToolTopK = 6,
                MaxToolResultChars = 7000
            }
        });

        return new RagQueryService(
            dbContext,
            embedding.Object,
            chatService,
            vectorRetriever.Object,
            new GroundedPromptBuilder(options),
            options,
            NullLogger<RagQueryService>.Instance,
            TimeProvider.System,
            new UserQuotaService(dbContext),
            retrieval);
    }

    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var postgresOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(
                "Host=unused;Database=unused;Username=unused;Password=unused",
                npgsql => npgsql.UseVector())
            .Options;
        using var postgresContext = new ApplicationDbContext(postgresOptions);

        var inMemoryOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"agentic-rag-test-{Guid.NewGuid()}")
            .UseModel(postgresContext.Model)
            .Options;

        return new ApplicationDbContext(inMemoryOptions);
    }

    private static async Task SetupSessionAsync(
        ApplicationDbContext dbContext,
        Guid userId,
        Guid sessionId,
        Guid subjectId)
    {
        if (!await dbContext.Users.AnyAsync(u => u.Id == userId))
        {
            dbContext.Users.Add(new ApplicationUser
            {
                Id = userId,
                UserName = $"user-{userId:N}@test.com",
                Email = $"user-{userId:N}@test.com",
                DisplayName = "Test User",
                CreatedAtUtc = DateTime.UtcNow,
                QuotaRemaining = 10
            });
        }

        dbContext.ChatSessions.Add(new ChatSession
        {
            Id = sessionId,
            UserId = userId,
            SubjectId = subjectId,
            Title = "Existing session"
        });
        await dbContext.SaveChangesAsync();
    }

    private sealed class ToolCallingChatService(
        string toolName,
        string toolArgument,
        string finalAnswer) : IChatCompletionService, IAgenticChatCompletionService
    {
        public string? ToolResult { get; private set; }

        public Task<string> CompleteAsync(
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(finalAnswer);

        public async IAsyncEnumerable<string> StreamWithToolsAsync(
            string systemPrompt,
            string userPrompt,
            IReadOnlyList<AgentToolDefinition> tools,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var tool = tools.Single(candidate => candidate.Name == toolName);
            var handler = Assert.IsType<Func<string, CancellationToken, Task<string>>>(tool.Handler);
            ToolResult = await handler(toolArgument, cancellationToken);

            yield return finalAnswer;
        }
    }
}
