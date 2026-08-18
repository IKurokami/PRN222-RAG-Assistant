using Microsoft.Extensions.Options;
using PRN222.RagAssistant.Infrastructure.Rag;
using Xunit;

namespace PRN222.RagAssistant.Tests;

public sealed class GroundedPromptBuilderTests
{
    private readonly GroundedPromptBuilder _sut;

    public GroundedPromptBuilderTests()
    {
        var options = Options.Create(new RagOptions
        {
            Retrieval = new RagOptions.RetrievalOptions
            {
                TopK = 5,
                MinimumSimilarityScore = 0.3,
                MaxContextChars = 4000,
                IncludeConversationHistory = true,
                HistoryTurns = 5,
                ExcerptChars = 240
            },
            Chat = new RagOptions.ChatOptions
            {
                NoEvidenceMessage = "Không tìm thấy."
            }
        });

        _sut = new GroundedPromptBuilder(options);
    }

    [Fact]
    public void Build_ReturnsNonEmptySystemPrompt()
    {
        var chunks = new List<RetrievedChunk>();
        var history = new List<ChatHistoryEntry>();

        var (systemPrompt, userPrompt) = _sut.Build("What is OOP?", chunks, history);

        Assert.False(string.IsNullOrWhiteSpace(systemPrompt));
        Assert.Contains("trả lời", systemPrompt);
    }

    [Fact]
    public void Build_UserPromptContainsQuestion()
    {
        var chunks = new List<RetrievedChunk>();
        var history = new List<ChatHistoryEntry>();

        var (_, userPrompt) = _sut.Build("What is inheritance?", chunks, history);

        Assert.Contains("What is inheritance?", userPrompt);
        Assert.Contains("Câu hỏi:", userPrompt);
    }

    [Fact]
    public void Build_IncludesContextBlock_WhenChunksProvided()
    {
        var chunks = new List<RetrievedChunk>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "OOP Basics", "OOP stands for object-oriented programming.", 1, null, 0.9)
        };
        var history = new List<ChatHistoryEntry>();

        var (_, userPrompt) = _sut.Build("What is OOP?", chunks, history);

        Assert.Contains("[CONTEXT]", userPrompt);
        Assert.Contains("[1]", userPrompt);
        Assert.Contains("OOP stands for object-oriented programming.", userPrompt);
        Assert.Contains("(Trang 1)", userPrompt);
    }

    [Fact]
    public void Build_IncludesHistory_WhenProvided()
    {
        var chunks = new List<RetrievedChunk>();
        var history = new List<ChatHistoryEntry>
        {
            new("User", "What is a class?"),
            new("Assistant", "A class is a blueprint.")
        };

        var (_, userPrompt) = _sut.Build("What is an object?", chunks, history);

        Assert.Contains("Lịch sử hội thoại", userPrompt);
        Assert.Contains("Người dùng: What is a class?", userPrompt);
        Assert.Contains("Trợ lý: A class is a blueprint.", userPrompt);
    }

    [Fact]
    public void Build_TruncatesChunks_WhenExceedsMaxContextChars()
    {
        var longContent = new string('x', 5000);
        var chunks = new List<RetrievedChunk>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "Doc", longContent, 1, null, 0.9),
            new(Guid.NewGuid(), Guid.NewGuid(), "Doc2", longContent, 2, null, 0.8)
        };
        var history = new List<ChatHistoryEntry>();

        var (_, userPrompt) = _sut.Build("Query", chunks, history);

        // Each chunk gets maxContextChars / chunkCount = 4000/2 = 2000 chars max
        // 5000 chars should be truncated to ~2000 + "..."
        Assert.Contains("[1]", userPrompt);
        Assert.Contains("[2]", userPrompt);
        Assert.Contains("...", userPrompt);
    }

    [Fact]
    public void Build_SkipsHistory_WhenDisabled()
    {
        var options = Options.Create(new RagOptions
        {
            Retrieval = new RagOptions.RetrievalOptions
            {
                IncludeConversationHistory = false,
                HistoryTurns = 5,
                MaxContextChars = 4000,
                ExcerptChars = 240
            },
            Chat = new RagOptions.ChatOptions()
        });
        var sut = new GroundedPromptBuilder(options);

        var history = new List<ChatHistoryEntry> { new("User", "Hello") };

        var (_, userPrompt) = sut.Build("Question", new List<RetrievedChunk>(), history);

        Assert.DoesNotContain("Lịch sử", userPrompt);
        Assert.DoesNotContain("Hello", userPrompt);
    }

    [Fact]
    public void Build_IncludesPageNumber_WhenProvided()
    {
        var chunks = new List<RetrievedChunk>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "Doc", "Content", 5, null, 0.9)
        };

        var (_, userPrompt) = _sut.Build("Q", chunks, new List<ChatHistoryEntry>());

        Assert.Contains("(Trang 5)", userPrompt);
    }

    [Fact]
    public void Build_IncludesSlideNumber_WhenProvided()
    {
        var chunks = new List<RetrievedChunk>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "Doc", "Content", null, 12, 0.9)
        };

        var (_, userPrompt) = _sut.Build("Q", chunks, new List<ChatHistoryEntry>());

        Assert.Contains("(Slide 12)", userPrompt);
    }

    [Fact]
    public void Build_EmptyLocation_WhenNoPageOrSlide()
    {
        var chunks = new List<RetrievedChunk>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "Doc", "Content", null, null, 0.9)
        };

        var (_, userPrompt) = _sut.Build("Q", chunks, new List<ChatHistoryEntry>());

        Assert.Contains("[1] ", userPrompt);
        Assert.DoesNotContain("Trang", userPrompt);
        Assert.DoesNotContain("Slide", userPrompt);
    }
}
