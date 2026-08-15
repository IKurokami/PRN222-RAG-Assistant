using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Application.Models;
using PRN222.RagAssistant.Infrastructure.Rag;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Domain.Enums;
using PRN222.RagAssistant.Features.Rag.Exceptions;

namespace PRN222.RagAssistant.Features.Rag;

public sealed class RagQueryService : IRagQueryService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ITextEmbeddingService _embeddingService;
    private readonly IChatCompletionService _chatService;
    private readonly IDocumentChunkRetriever _retriever;
    private readonly GroundedPromptBuilder _promptBuilder;
    private readonly RagOptions _options;
    private readonly ILogger<RagQueryService> _logger;
    private readonly TimeProvider _clock;

    public RagQueryService(
        ApplicationDbContext dbContext,
        ITextEmbeddingService embeddingService,
        IChatCompletionService chatService,
        IDocumentChunkRetriever retriever,
        GroundedPromptBuilder promptBuilder,
        IOptions<RagOptions> options,
        ILogger<RagQueryService> logger,
        TimeProvider clock)
    {
        _dbContext = dbContext;
        _embeddingService = embeddingService;
        _chatService = chatService;
        _retriever = retriever;
        _promptBuilder = promptBuilder;
        _options = options.Value;
        _logger = logger;
        _clock = clock;
    }

    public async Task<RagAnswer> AskAsync(
        Guid userId,
        Guid chatSessionId,
        string question,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException("Question cannot be null or whitespace.", nameof(question));
        }

        var session = await _dbContext.ChatSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == chatSessionId && s.UserId == userId, cancellationToken)
            ?? throw new ChatSessionNotFoundException(chatSessionId, userId);

        var userMessage = await PersistUserMessageAsync(session.Id, question, cancellationToken);

        var questionEmbedding = await _embeddingService.EmbedAsync(question, cancellationToken);
        var allChunks = await _retriever.SearchAsync(questionEmbedding, cancellationToken);

        var topChunks = allChunks
            .Where(c => c.SimilarityScore >= _options.Retrieval.MinimumSimilarityScore)
            .Take(_options.Retrieval.TopK)
            .ToList();

        cancellationToken.ThrowIfCancellationRequested();

        string answer;
        IReadOnlyList<RagCitation> citations;

        if (topChunks.Count == 0)
        {
            answer = _options.Chat.NoEvidenceMessage;
            citations = Array.Empty<RagCitation>();
        }
        else
        {
            var history = await LoadRecentHistoryAsync(session.Id, cancellationToken);
            var (systemPrompt, userPrompt) = _promptBuilder.Build(question, topChunks, history);
            answer = await _chatService.CompleteAsync(systemPrompt, userPrompt, cancellationToken);
            citations = BuildCitations(topChunks);
        }

        var assistantMessage = await PersistAssistantMessageAsync(session.Id, answer, citations, cancellationToken);

        await EnsureSessionTitleAsync(session, question, cancellationToken);

        _logger.LogInformation(
            "RAG query completed. SessionId={SessionId}, UserId={UserId}, ChunksFound={ChunkCount}, Citations={CitationCount}",
            session.Id, userId, topChunks.Count, citations.Count);

        return new RagAnswer(
            session.Id,
            userMessage.Id,
            assistantMessage.Id,
            answer,
            citations);
    }

    private async Task<ChatMessage> PersistUserMessageAsync(
        Guid sessionId,
        string content,
        CancellationToken cancellationToken)
    {
        var message = new ChatMessage
        {
            Id = Guid.CreateVersion7(),
            ChatSessionId = sessionId,
            Role = ChatMessageRole.User,
            Content = content,
            CreatedAtUtc = _clock.GetUtcNow().DateTime
        };

        _dbContext.ChatMessages.Add(message);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return message;
    }

    private async Task<ChatMessage> PersistAssistantMessageAsync(
        Guid sessionId,
        string content,
        IReadOnlyList<RagCitation> citations,
        CancellationToken cancellationToken)
    {
        var message = new ChatMessage
        {
            Id = Guid.CreateVersion7(),
            ChatSessionId = sessionId,
            Role = ChatMessageRole.Assistant,
            Content = content,
            CreatedAtUtc = _clock.GetUtcNow().DateTime
        };

        _dbContext.ChatMessages.Add(message);

        for (int i = 0; i < citations.Count; i++)
        {
            var citation = citations[i];
            _dbContext.MessageCitations.Add(new MessageCitation
            {
                Id = Guid.CreateVersion7(),
                ChatMessageId = message.Id,
                DocumentChunkId = citation.DocumentChunkId,
                Rank = i + 1
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return message;
    }

    private async Task<IReadOnlyList<ChatHistoryEntry>> LoadRecentHistoryAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var history = await _dbContext.ChatMessages
            .AsNoTracking()
            .Where(m => m.ChatSessionId == sessionId)
            .OrderByDescending(m => m.CreatedAtUtc)
            .Take(_options.Retrieval.HistoryTurns * 2)
            .OrderBy(m => m.CreatedAtUtc)
            .Select(m => new ChatHistoryEntry(m.Role.ToString(), m.Content))
            .ToListAsync(cancellationToken);

        return history;
    }

    private async Task EnsureSessionTitleAsync(
        Domain.Entities.ChatSession session,
        string firstQuestion,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(session.Title))
            return;

        var title = firstQuestion.Length > 50
            ? firstQuestion.Substring(0, 47) + "..."
            : firstQuestion;

        await _dbContext.ChatSessions
            .Where(s => s.Id == session.Id)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.Title, title)
                      .SetProperty(x => x.UpdatedAtUtc, _clock.GetUtcNow().DateTime),
                cancellationToken);
    }

    private IReadOnlyList<RagCitation> BuildCitations(IReadOnlyList<RetrievedChunk> chunks)
    {
        return chunks.Select((chunk, index) => new RagCitation(
            chunk.DocumentId,
            chunk.DocumentChunkId,
            chunk.DocumentTitle,
            index + 1,
            TruncateExcerpt(chunk.Content, _options.Retrieval.ExcerptChars),
            chunk.PageNumber,
            chunk.SlideNumber))
            .ToList();
    }

    private static string TruncateExcerpt(string content, int maxChars)
    {
        if (string.IsNullOrEmpty(content) || content.Length <= maxChars)
            return content;

        var truncated = content.Substring(0, maxChars);
        var lastSpace = truncated.LastIndexOf(' ');
        if (lastSpace > maxChars / 2)
            truncated = truncated.Substring(0, lastSpace);

        return truncated + "...";
    }
}
