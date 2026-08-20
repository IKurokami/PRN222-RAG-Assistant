using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Application.Models;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Domain.Enums;
using PRN222.RagAssistant.Infrastructure.Rag.Exceptions;

namespace PRN222.RagAssistant.Infrastructure.Rag;

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
        Guid? subjectId = null,
        CancellationToken cancellationToken = default)
    {
        ValidateQuestion(question);

        var session = await _dbContext.ChatSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == chatSessionId && s.UserId == userId, cancellationToken)
            ?? throw new ChatSessionNotFoundException(chatSessionId, userId);

        // Enforce subject scope: if session has a bound SubjectId, reject conflicting subjectId.
        if (session.SubjectId.HasValue && subjectId.HasValue && session.SubjectId.Value != subjectId.Value)
        {
            throw new ArgumentException(
                $"Provided subjectId '{subjectId.Value}' does not match session SubjectId '{session.SubjectId.Value}'.",
                nameof(subjectId));
        }

        var effectiveSubjectId = session.SubjectId ?? subjectId;

        // Load history BEFORE persisting current message to avoid duplicating the question.
        var history = await LoadRecentHistoryAsync(session.Id, cancellationToken);
        var queryResult = await GenerateAnswerAsync(
            question,
            effectiveSubjectId,
            history,
            cancellationToken);

        // Persist messages AFTER processing to avoid including current question in history.
        var userMessage = await PersistUserMessageAsync(session.Id, question, cancellationToken);
        var assistantMessage = await PersistAssistantMessageAsync(
            session.Id,
            queryResult.Answer,
            queryResult.Citations,
            cancellationToken);

        await EnsureSessionTitleAsync(session, question, cancellationToken);

        _logger.LogInformation(
            "RAG query completed. SessionId={SessionId}, UserId={UserId}, ChunksFound={ChunkCount}, Citations={CitationCount}",
            session.Id,
            userId,
            queryResult.ChunkCount,
            queryResult.Citations.Count);

        return new RagAnswer(
            session.Id,
            userMessage.Id,
            assistantMessage.Id,
            queryResult.Answer,
            queryResult.Citations);
    }

    public async Task<RagQueryResult> AskStatelessAsync(
        string question,
        Guid subjectId,
        CancellationToken cancellationToken = default)
    {
        ValidateQuestion(question);

        var queryResult = await GenerateAnswerAsync(
            question,
            subjectId,
            Array.Empty<ChatHistoryEntry>(),
            cancellationToken);

        _logger.LogInformation(
            "Stateless RAG query completed. SubjectId={SubjectId}, ChunksFound={ChunkCount}, Citations={CitationCount}",
            subjectId,
            queryResult.ChunkCount,
            queryResult.Citations.Count);

        return new RagQueryResult(queryResult.Answer, queryResult.Citations);
    }

    private async Task<(string Answer, IReadOnlyList<RagCitation> Citations, int ChunkCount)> GenerateAnswerAsync(
        string question,
        Guid? subjectId,
        IReadOnlyList<ChatHistoryEntry> history,
        CancellationToken cancellationToken)
    {
        var questionEmbedding = await _embeddingService.EmbedAsync(question, cancellationToken);
        var allChunks = await _retriever.SearchAsync(questionEmbedding, subjectId, cancellationToken);

        var topChunks = allChunks
            .Where(c => c.SimilarityScore >= _options.Retrieval.MinimumSimilarityScore)
            .Take(_options.Retrieval.TopK)
            .ToList();

        cancellationToken.ThrowIfCancellationRequested();

        if (topChunks.Count == 0)
        {
            return (
                _options.Chat.NoEvidenceMessage,
                Array.Empty<RagCitation>(),
                0);
        }

        var (systemPrompt, userPrompt) = _promptBuilder.Build(question, topChunks, history);
        var answer = await _chatService.CompleteAsync(systemPrompt, userPrompt, cancellationToken);
        var citations = ParseCitationsFromAnswer(answer, topChunks);

        return (answer, citations, topChunks.Count);
    }

    private static void ValidateQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException("Question cannot be null or whitespace.", nameof(question));
        }
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
            CreatedAtUtc = DateTime.SpecifyKind(_clock.GetUtcNow().DateTime, DateTimeKind.Utc)
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
            CreatedAtUtc = DateTime.SpecifyKind(_clock.GetUtcNow().DateTime, DateTimeKind.Utc)
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
                      .SetProperty(x => x.UpdatedAtUtc, DateTime.SpecifyKind(_clock.GetUtcNow().DateTime, DateTimeKind.Utc)),
                cancellationToken);
    }

    private IReadOnlyList<RagCitation> ParseCitationsFromAnswer(string answer, IReadOnlyList<RetrievedChunk> chunks)
    {
        if (string.IsNullOrEmpty(answer) || chunks.Count == 0)
            return Array.Empty<RagCitation>();

        // Parse citation markers [n] from the answer.
        var citationPattern = new System.Text.RegularExpressions.Regex(@"\[(\d+)\]");
        var matches = citationPattern.Matches(answer);

        if (matches.Count == 0)
            return Array.Empty<RagCitation>();

        var usedIndices = new HashSet<int>();
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            if (int.TryParse(match.Groups[1].Value, out int index) && index > 0 && index <= chunks.Count)
            {
                usedIndices.Add(index);
            }
        }

        if (usedIndices.Count == 0)
            return Array.Empty<RagCitation>();

        // Sort indices to maintain original ranking.
        var sortedIndices = usedIndices.OrderBy(i => i).ToList();
        var citations = new List<RagCitation>();

        for (int rank = 0; rank < sortedIndices.Count; rank++)
        {
            var index = sortedIndices[rank] - 1; // Convert to 0-based.
            var chunk = chunks[index];
            citations.Add(new RagCitation(
                chunk.DocumentId,
                chunk.DocumentChunkId,
                chunk.DocumentTitle,
                rank + 1,
                TruncateExcerpt(chunk.Content, _options.Retrieval.ExcerptChars),
                chunk.PageNumber,
                chunk.SlideNumber));
        }

        return citations;
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

    public async Task<Guid> GetOrCreateUserSessionAsync(
        Guid userId,
        Guid? subjectId = null,
        CancellationToken cancellationToken = default)
    {
        var targetSubjectId = subjectId;
        if (!targetSubjectId.HasValue)
        {
            targetSubjectId = await _dbContext.Subjects
                .Where(s => s.IsActive)
                .OrderBy(s => s.Code)
                .Select(s => (Guid?)s.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var session = await _dbContext.ChatSessions
            .Where(s => s.UserId == userId && (targetSubjectId == null || s.SubjectId == targetSubjectId))
            .OrderByDescending(s => s.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null)
        {
            var nowUtc = DateTime.SpecifyKind(_clock.GetUtcNow().DateTime, DateTimeKind.Utc);
            session = new ChatSession
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                SubjectId = targetSubjectId,
                Title = string.Empty,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc
            };
            _dbContext.ChatSessions.Add(session);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        else if (!session.SubjectId.HasValue && targetSubjectId.HasValue)
        {
            session.SubjectId = targetSubjectId;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return session.Id;
    }
}
