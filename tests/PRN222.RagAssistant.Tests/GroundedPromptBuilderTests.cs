using PRN222.RagAssistant.Infrastructure.Rag;
using Xunit;

namespace PRN222.RagAssistant.Tests;

public sealed class GroundedPromptBuilderTests
{
    [Fact]
    public void BuildSystemPrompt_ReturnsNonEmptyPrompt()
    {
        var builder = CreateBuilder();

        var systemPrompt = builder.BuildSystemPrompt();

        Assert.False(string.IsNullOrWhiteSpace(systemPrompt));
        Assert.Contains("PRN222", systemPrompt);
    }

    [Fact]
    public void Build_WithChunks_IncludesContextMarkers()
    {
        var builder = CreateBuilder();
        var chunks = new[]
        {
            new RetrievedChunk(
                DocumentChunkId: Guid.NewGuid(),
                DocumentId: Guid.NewGuid(),
                DocumentTitle: "Test Doc",
                Content: "This is the first chunk content about OOP concepts.",
                PageNumber: 1,
                SlideNumber: null,
                SimilarityScore: 0.95)
        };
        var history = Array.Empty<ChatHistoryEntry>();

        var (systemPrompt, userPrompt) = builder.Build("What is OOP?", chunks, history);

        Assert.Contains("[CONTEXT]", userPrompt);
        Assert.Contains("[/CONTEXT]", userPrompt);
        Assert.Contains("[1]", userPrompt);
        Assert.Contains("(Trang 1)", userPrompt);
    }

    [Fact]
    public void Build_WithSlideNumber_FormatsCorrectly()
    {
        var builder = CreateBuilder();
        var chunks = new[]
        {
            new RetrievedChunk(
                DocumentChunkId: Guid.NewGuid(),
                DocumentId: Guid.NewGuid(),
                DocumentTitle: "Test PPT",
                Content: "Slide content",
                PageNumber: null,
                SlideNumber: 5,
                SimilarityScore: 0.9)
        };

        var (_, userPrompt) = builder.Build("Slide content", chunks, Array.Empty<ChatHistoryEntry>());

        Assert.Contains("(Slide 5)", userPrompt);
    }

    [Fact]
    public void Build_WithHistory_IncludesHistorySection()
    {
        var builder = CreateBuilder();
        var chunks = Array.Empty<RetrievedChunk>();
        var history = new[]
        {
            new ChatHistoryEntry("User", "What is inheritance?"),
            new ChatHistoryEntry("Assistant", "Inheritance is when a class derives from another class.")
        };

        var (_, userPrompt) = builder.Build("Tell me more", chunks, history);

        Assert.Contains("Lịch sử hội thoại gần đây", userPrompt);
        Assert.Contains("Người dùng: What is inheritance?", userPrompt);
        Assert.Contains("Trợ lý: Inheritance is when a class derives from another class.", userPrompt);
    }

    [Fact]
    public void BuildNoEvidenceUserPrompt_ContainsOnlyQuestion()
    {
        var builder = CreateBuilder();

        var userPrompt = builder.BuildNoEvidenceUserPrompt("What is the meaning of life?");

        Assert.Contains("Câu hỏi: What is the meaning of life?", userPrompt);
        Assert.DoesNotContain("[CONTEXT]", userPrompt);
    }

    [Fact]
    public void Build_WithEmptyChunks_ReturnsNoEvidenceMessage()
    {
        var builder = CreateBuilder();
        var chunks = Array.Empty<RetrievedChunk>();

        var (_, userPrompt) = builder.Build("Any question", chunks, Array.Empty<ChatHistoryEntry>());

        Assert.Contains("Không có tài liệu liên quan", userPrompt);
    }

    private static GroundedPromptBuilder CreateBuilder()
    {
        var ragOptions = new PRN222.RagAssistant.Infrastructure.Rag.RagOptions
        {
            Retrieval = new PRN222.RagAssistant.Infrastructure.Rag.RagOptions.RetrievalOptions
            {
                TopK = 5,
                MinimumSimilarityScore = 0.3,
                MaxContextChars = 4000,
                HistoryTurns = 5,
                ExcerptChars = 240
            },
            Chat = new PRN222.RagAssistant.Infrastructure.Rag.RagOptions.ChatOptions
            {
                NoEvidenceMessage = "Tôi chỉ có thể trả lời dựa trên tài liệu PRN222 đã được index."
            }
        };

        return new GroundedPromptBuilder(Microsoft.Extensions.Options.Options.Create(ragOptions));
    }
}
