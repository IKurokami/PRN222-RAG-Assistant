using Moq;
using PRN222.RagAssistant.Infrastructure.Rag;
using Xunit;

namespace PRN222.RagAssistant.Tests;

public sealed class RagQueryServiceTests
{
    [Fact]
    public async Task AskAsync_ThrowsArgumentException_WhenQuestionIsNull()
    {
        var (service, _, _, _) = CreateTestService();
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

    [Fact(Skip = "Requires integration test with real DbContext (InMemory doesn't support Identity)")]
    public async Task AskAsync_ThrowsChatSessionNotFoundException_WhenSessionDoesNotExist()
    {
        var (service, _, _, _) = CreateTestService();

        await Assert.ThrowsAsync<PRN222.RagAssistant.Features.Rag.Exceptions.ChatSessionNotFoundException>(
            () => service.AskAsync(Guid.NewGuid(), Guid.NewGuid(), "What is OOP?"));
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

        // Create service to use its BuildCitations method via reflection or create the citations manually
        var (service, _, _, _) = CreateTestService();
        var method = typeof(PRN222.RagAssistant.Features.Rag.RagQueryService)
            .GetMethod("BuildCitations", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var citations = method?.Invoke(service, new object[] { chunks }) as IReadOnlyList<PRN222.RagAssistant.Application.Models.RagCitation>;

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
        var method = typeof(PRN222.RagAssistant.Features.Rag.RagQueryService)
            .GetMethod("BuildCitations", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var citations = method?.Invoke(service, new object[] { chunks }) as IReadOnlyList<PRN222.RagAssistant.Application.Models.RagCitation>;

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

        // Create service with custom ExcerptChars = 50
        var embeddingMock = new Moq.Mock<PRN222.RagAssistant.Application.Abstractions.ITextEmbeddingService>();
        var chatMock = new Moq.Mock<PRN222.RagAssistant.Application.Abstractions.IChatCompletionService>();
        var retriever = new TestableDocumentChunkRetriever();

        var ragOptions = new Infrastructure.Rag.RagOptions
        {
            Retrieval = new Infrastructure.Rag.RagOptions.RetrievalOptions
            {
                TopK = 5,
                MinimumSimilarityScore = 0.3,
                MaxContextChars = 4000,
                HistoryTurns = 5,
                ExcerptChars = 50
            },
            Chat = new Infrastructure.Rag.RagOptions.ChatOptions
            {
                NoEvidenceMessage = "No evidence."
            }
        };

        var promptBuilder = new PRN222.RagAssistant.Infrastructure.Rag.GroundedPromptBuilder(
            Microsoft.Extensions.Options.Options.Create(ragOptions));

        var service = new PRN222.RagAssistant.Features.Rag.RagQueryService(
            null!,
            embeddingMock.Object,
            chatMock.Object,
            retriever,
            promptBuilder,
            Microsoft.Extensions.Options.Options.Create(ragOptions),
            Moq.Mock.Of<Microsoft.Extensions.Logging.ILogger<PRN222.RagAssistant.Features.Rag.RagQueryService>>(),
            TimeProvider.System);

        var method = typeof(PRN222.RagAssistant.Features.Rag.RagQueryService)
            .GetMethod("BuildCitations", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var citations = method?.Invoke(service, new object[] { chunks }) as IReadOnlyList<PRN222.RagAssistant.Application.Models.RagCitation>;

        Assert.NotNull(citations);
        Assert.True(citations[0].Excerpt.Length <= 53); // 50 + "..."
    }

    [Fact(Skip = "Requires complex InMemory + IdentityDbContext setup; cancellation is covered by integration testing")]
    public async Task AskAsync_ThrowsOperationCanceledException_WhenTokenCancelled_BeforeEmbedding()
    {
        // This test requires a full DbContext with Identity setup
        // Skipped for now as it's more of an integration test than unit test
        await Task.CompletedTask;
    }

    private static (PRN222.RagAssistant.Features.Rag.RagQueryService Service,
        Moq.Mock<PRN222.RagAssistant.Application.Abstractions.ITextEmbeddingService> EmbeddingMock,
        Moq.Mock<PRN222.RagAssistant.Application.Abstractions.IChatCompletionService> ChatMock,
        TestableDocumentChunkRetriever Retriever)
        CreateTestService()
    {
        var embeddingMock = new Moq.Mock<PRN222.RagAssistant.Application.Abstractions.ITextEmbeddingService>();
        var chatMock = new Moq.Mock<PRN222.RagAssistant.Application.Abstractions.IChatCompletionService>();
        var retriever = new TestableDocumentChunkRetriever();

        var ragOptions = new Infrastructure.Rag.RagOptions
        {
            Retrieval = new Infrastructure.Rag.RagOptions.RetrievalOptions
            {
                TopK = 5,
                MinimumSimilarityScore = 0.3,
                MaxContextChars = 4000,
                HistoryTurns = 5,
                ExcerptChars = 240
            },
            Chat = new Infrastructure.Rag.RagOptions.ChatOptions
            {
                NoEvidenceMessage = "Không tìm thấy thông tin phù hợp."
            }
        };

        var promptBuilder = new PRN222.RagAssistant.Infrastructure.Rag.GroundedPromptBuilder(
            Microsoft.Extensions.Options.Options.Create(ragOptions));

        embeddingMock.Setup(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.1f, 0.2f });
        chatMock.Setup(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Mocked answer");
        retriever.ChunksToReturn = Array.Empty<RetrievedChunk>();

        var service = new PRN222.RagAssistant.Features.Rag.RagQueryService(
            null!,
            embeddingMock.Object,
            chatMock.Object,
            retriever,
            promptBuilder,
            Microsoft.Extensions.Options.Options.Create(ragOptions),
            Moq.Mock.Of<Microsoft.Extensions.Logging.ILogger<PRN222.RagAssistant.Features.Rag.RagQueryService>>(),
            TimeProvider.System);

        return (service, embeddingMock, chatMock, retriever);
    }
}

internal sealed class TestableDocumentChunkRetriever : PRN222.RagAssistant.Infrastructure.Rag.IDocumentChunkRetriever
{
    public IReadOnlyList<RetrievedChunk> ChunksToReturn { get; set; } = Array.Empty<RetrievedChunk>();

    public Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        float[] questionEmbedding,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ChunksToReturn);
    }
}
