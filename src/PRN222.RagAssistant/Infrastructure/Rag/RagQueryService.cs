using System.Collections.Concurrent;
using System.Text;
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
    private readonly IAgenticRetrievalService? _agenticRetrievalService;

    public RagQueryService(
        ApplicationDbContext dbContext,
        ITextEmbeddingService embeddingService,
        IChatCompletionService chatService,
        IDocumentChunkRetriever retriever,
        GroundedPromptBuilder promptBuilder,
        IOptions<RagOptions> options,
        ILogger<RagQueryService> logger,
        TimeProvider clock,
        IAgenticRetrievalService? agenticRetrievalService = null)
    {
        _dbContext = dbContext;
        _embeddingService = embeddingService;
        _chatService = chatService;
        _retriever = retriever;
        _promptBuilder = promptBuilder;
        _options = options.Value;
        _logger = logger;
        _clock = clock;
        _agenticRetrievalService = agenticRetrievalService;
    }

    public async Task<RagAnswer> AskAsync(
        Guid userId,
        Guid chatSessionId,
        string question,
        Guid? subjectId = null,
        CancellationToken cancellationToken = default)
    {
        RagAnswer? completedAnswer = null;

        await foreach (var streamEvent in AskStreamAsync(
                           userId,
                           chatSessionId,
                           question,
                           subjectId,
                           cancellationToken))
        {
            if (streamEvent is RagCompletedEvent completed)
            {
                completedAnswer = completed.Answer;
            }
        }

        return completedAnswer
            ?? throw new InvalidOperationException("RAG stream ended without a completed answer.");
    }

    public async IAsyncEnumerable<RagStreamEvent> AskStreamAsync(
        Guid userId,
        Guid chatSessionId,
        string question,
        Guid? subjectId = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        ValidateQuestion(question);

        var session = await _dbContext.ChatSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == chatSessionId && s.UserId == userId, cancellationToken)
            ?? throw new ChatSessionNotFoundException(chatSessionId, userId);

        if (session.SubjectId.HasValue && subjectId.HasValue && session.SubjectId.Value != subjectId.Value)
        {
            throw new ArgumentException(
                $"Provided subjectId '{subjectId.Value}' does not match session SubjectId '{session.SubjectId.Value}'.",
                nameof(subjectId));
        }

        var effectiveSubjectId = session.SubjectId ?? subjectId;
        var history = await LoadRecentHistoryAsync(session.Id, cancellationToken);

        string answerText;
        IReadOnlyList<RagCitation> citations;
        int chunkCount;

        var canUseAgenticRag =
            _options.Agentic.Enabled
            && effectiveSubjectId.HasValue
            && _agenticRetrievalService is not null
            && _chatService is IAgenticChatCompletionService;

        if (canUseAgenticRag)
        {
            var agenticChat = (IAgenticChatCompletionService)_chatService;
            var evidence = new EvidenceCollector(_options.Agentic.MaxToolResultChars);
            var toolEvents = new ConcurrentQueue<RagToolCallEvent>();
            var toolSession = new AgentToolSession(
                _agenticRetrievalService!,
                effectiveSubjectId!.Value,
                evidence,
                toolEvents,
                _options.Agentic.ToolTopK);

            yield return new RagToolCallEvent(
                "agent-plan",
                "agentic_rag",
                "running",
                "AI đang chọn cách tra cứu tài liệu",
                "Model có thể tự gọi hybrid search, keyword search, lấy ngữ cảnh lân cận hoặc liệt kê tài liệu.");

            var tools = CreateAgentTools(toolSession);
            var systemPrompt = BuildAgenticSystemPrompt();
            var userPrompt = BuildAgenticUserPrompt(question, history);
            var answerBuilder = new StringBuilder();

            await foreach (var delta in agenticChat.StreamWithToolsAsync(
                               systemPrompt,
                               userPrompt,
                               tools,
                               cancellationToken))
            {
                while (toolEvents.TryDequeue(out var toolEvent))
                {
                    yield return toolEvent;
                }

                // Never expose a direct answer before a successful retrieval tool has
                // produced either chunk evidence or trusted document metadata evidence.
                if (!evidence.HasEvidence)
                {
                    continue;
                }

                answerBuilder.Append(delta);
                yield return new RagDeltaEvent(delta);
            }

            while (toolEvents.TryDequeue(out var remainingToolEvent))
            {
                yield return remainingToolEvent;
            }

            var rejectedUngroundedAnswer = false;
            if (!evidence.HasEvidence)
            {
                answerText = _options.Chat.NoEvidenceMessage;
                citations = Array.Empty<RagCitation>();
                chunkCount = 0;
                yield return new RagDeltaEvent(answerText);
            }
            else
            {
                answerText = answerBuilder.ToString().Trim();
                chunkCount = evidence.Count;

                if (string.IsNullOrWhiteSpace(answerText))
                {
                    answerText = _options.Chat.NoEvidenceMessage;
                    citations = Array.Empty<RagCitation>();
                }
                else
                {
                    citations = ParseCitationsFromAnswer(answerText, evidence.Chunks);

                    // Metadata-only answers from list_documents cannot point at a chunk,
                    // but any answer synthesized from retrieved chunks must reference at
                    // least one valid marker before it can be persisted as grounded output.
                    if (evidence.RequiresCitations
                        && citations.Count == 0
                        && !string.Equals(
                            answerText,
                            _options.Chat.NoEvidenceMessage,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        rejectedUngroundedAnswer = true;
                        answerText = _options.Chat.NoEvidenceMessage;
                        citations = Array.Empty<RagCitation>();

                        _logger.LogWarning(
                            "Agentic RAG answer rejected because chunk evidence was used without a valid citation marker. SessionId={SessionId}, UserId={UserId}, ChunkCount={ChunkCount}",
                            session.Id,
                            userId,
                            chunkCount);
                    }
                }
            }

            yield return new RagToolCallEvent(
                "agent-plan",
                "agentic_rag",
                "completed",
                $"Agent hoàn tất với {evidence.TotalEvidenceCount} mục bằng chứng",
                rejectedUngroundedAnswer
                    ? "Câu trả lời không có citation marker hợp lệ nên đã bị chặn."
                    : citations.Count > 0
                        ? $"Câu trả lời sử dụng {citations.Count} trích dẫn."
                        : "Câu trả lời dựa trên metadata tài liệu hoặc không có chunk cần trích dẫn.");
        }
        else
        {
            yield return new RagToolCallEvent(
                "classic-retrieval",
                "search_pgvector",
                "running",
                "Đang tìm kiếm tài liệu",
                "Fallback RAG: embedding + pgvector theo phạm vi môn học.");

            var prepared = await PrepareGroundedQueryAsync(
                question,
                effectiveSubjectId,
                history,
                cancellationToken);

            if (prepared.Chunks.Count == 0)
            {
                answerText = _options.Chat.NoEvidenceMessage;
                citations = Array.Empty<RagCitation>();
                chunkCount = 0;

                yield return new RagToolCallEvent(
                    "classic-retrieval",
                    "search_pgvector",
                    "completed",
                    "Không tìm thấy tài liệu phù hợp");
                yield return new RagDeltaEvent(answerText);
            }
            else
            {
                chunkCount = prepared.Chunks.Count;
                yield return new RagToolCallEvent(
                    "classic-retrieval",
                    "search_pgvector",
                    "completed",
                    $"Đã tìm thấy {chunkCount} đoạn tài liệu phù hợp");

                // Preserve true SSE streaming. C# async iterators cannot yield from inside
                // a try/catch (CS1626), so only MoveNextAsync/CompleteAsync is wrapped and
                // each successful token is yielded after leaving the catch boundary.
                var answerBuilder = new StringBuilder();
                AiProviderRateLimitException? rateLimitException = null;

                if (_chatService is IStreamingChatCompletionService streamingChat)
                {
                    await using var enumerator = streamingChat.StreamAsync(
                            prepared.SystemPrompt!,
                            prepared.UserPrompt!,
                            cancellationToken)
                        .GetAsyncEnumerator(cancellationToken);

                    while (true)
                    {
                        bool hasNext;
                        string delta;

                        try
                        {
                            hasNext = await enumerator.MoveNextAsync();
                            delta = hasNext ? enumerator.Current : string.Empty;
                        }
                        catch (AiProviderRateLimitException ex)
                        {
                            rateLimitException = ex;
                            break;
                        }

                        if (!hasNext)
                        {
                            break;
                        }

                        answerBuilder.Append(delta);
                        yield return new RagDeltaEvent(delta);
                    }
                }
                else
                {
                    string? completed = null;

                    try
                    {
                        completed = await _chatService.CompleteAsync(
                            prepared.SystemPrompt!,
                            prepared.UserPrompt!,
                            cancellationToken);
                    }
                    catch (AiProviderRateLimitException ex)
                    {
                        rateLimitException = ex;
                    }

                    if (rateLimitException is null && completed is not null)
                    {
                        answerBuilder.Append(completed);
                        yield return new RagDeltaEvent(completed);
                    }
                }

                if (rateLimitException is not null)
                {
                    _logger.LogWarning(
                        rateLimitException,
                        "AI provider rate-limited during chat generation. Provider={Provider}, SessionId={SessionId}, UserId={UserId}",
                        rateLimitException.ProviderName,
                        session.Id,
                        userId);
                    yield return new RagErrorEvent(
                        "AI_PROVIDER_RATE_LIMITED",
                        "Dịch vụ AI hiện đang quá tải hoặc đã đạt giới hạn yêu cầu. Vui lòng thử lại sau một lúc.");
                    yield break;
                }

                answerText = answerBuilder.ToString().Trim();
                citations = ParseCitationsFromAnswer(answerText, prepared.Chunks);
            }
        }

        // Persist only after model/retrieval processing has completed successfully.
        // A cancelled/broken stream therefore does not leave a partial assistant message.
        var userMessage = await PersistUserMessageAsync(session.Id, question, cancellationToken);
        var assistantMessage = await PersistAssistantMessageAsync(
            session.Id,
            answerText,
            citations,
            cancellationToken);

        await EnsureSessionTitleAsync(session, question, cancellationToken);

        var answer = new RagAnswer(
            session.Id,
            userMessage.Id,
            assistantMessage.Id,
            answerText,
            citations);

        _logger.LogInformation(
            "RAG query completed. SessionId={SessionId}, UserId={UserId}, Agentic={Agentic}, ChunksFound={ChunkCount}, Citations={CitationCount}",
            session.Id,
            userId,
            canUseAgenticRag,
            chunkCount,
            citations.Count);

        yield return new RagCitationsEvent(citations);
        yield return new RagCompletedEvent(answer);
    }

    public async Task<RagQueryResult> AskStatelessAsync(
        string question,
        Guid subjectId,
        CancellationToken cancellationToken = default)
    {
        ValidateQuestion(question);

        // Evaluation intentionally keeps the deterministic retrieval pipeline so it can
        // serve as a stable baseline against the interactive agentic chat experience.
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
        var prepared = await PrepareGroundedQueryAsync(
            question,
            subjectId,
            history,
            cancellationToken);

        if (prepared.Chunks.Count == 0)
        {
            return (_options.Chat.NoEvidenceMessage, Array.Empty<RagCitation>(), 0);
        }

        var answer = await _chatService.CompleteAsync(
            prepared.SystemPrompt!,
            prepared.UserPrompt!,
            cancellationToken);
        var citations = ParseCitationsFromAnswer(answer, prepared.Chunks);

        return (answer, citations, prepared.Chunks.Count);
    }

    private async Task<PreparedGroundedQuery> PrepareGroundedQueryAsync(
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

        // Classic fallback retains the previous contextual expansion behavior.
        if (topChunks.Count == 0 && history.Count > 0)
        {
            var recentUserQuestion = history
                .Where(h => string.Equals(h.Role, "user", StringComparison.OrdinalIgnoreCase))
                .Select(h => h.Content)
                .LastOrDefault();

            if (!string.IsNullOrWhiteSpace(recentUserQuestion)
                && !string.Equals(recentUserQuestion.Trim(), question.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                var contextualQuery = $"{recentUserQuestion} {question}";
                var contextualEmbedding = await _embeddingService.EmbedAsync(contextualQuery, cancellationToken);
                var contextualChunks = await _retriever.SearchAsync(
                    contextualEmbedding,
                    subjectId,
                    cancellationToken);

                topChunks = contextualChunks
                    .Where(c => c.SimilarityScore >= _options.Retrieval.MinimumSimilarityScore)
                    .Take(_options.Retrieval.TopK)
                    .ToList();
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (topChunks.Count == 0)
        {
            return new PreparedGroundedQuery(topChunks, null, null);
        }

        var (systemPrompt, userPrompt) = _promptBuilder.Build(question, topChunks, history);
        return new PreparedGroundedQuery(topChunks, systemPrompt, userPrompt);
    }

    private string BuildAgenticSystemPrompt() => $"""
        Bạn là AI Agent RAG chuyên trả lời dựa trên tài liệu học tập nội bộ.

        QUY TẮC BẮT BUỘC:
        1. Chỉ sử dụng bằng chứng do các tool retrieval cung cấp. Không dùng kiến thức bên ngoài để khẳng định dữ kiện.
        2. Hãy chủ động viết lại câu hỏi follow-up thành truy vấn độc lập trước khi gọi tool.
        3. Ưu tiên `search_documents` cho tìm kiếm thông thường. Tool này kết hợp semantic pgvector + PostgreSQL full-text bằng Reciprocal Rank Fusion.
        4. Dùng `keyword_search` khi cần tên riêng, mã, phiên bản, năm, thuật ngữ hoặc chuỗi chính xác.
        5. Khi một đoạn có vẻ thiếu ngữ cảnh trước/sau, gọi `get_chunk_context` với chunk_id được tool tìm kiếm trả về.
        6. Có thể gọi tool nhiều lần với các truy vấn khác nhau nếu câu hỏi cần tổng hợp nhiều nguồn.
        7. Dùng `list_documents` khi người dùng hỏi có những tài liệu nào hoặc cần xác định đúng tên tài liệu. Metadata trả về từ tool này là bằng chứng hợp lệ cho câu hỏi liệt kê tài liệu và không cần marker chunk.
        8. Mỗi đoạn bằng chứng có marker [n]. Khi trả lời bằng nội dung chunk, đặt đúng marker [n] ngay sau dữ kiện được hỗ trợ bởi đoạn đó.
        9. Nếu các tool không tìm thấy bằng chứng đủ để trả lời, trả lời đúng tinh thần: "{_options.Chat.NoEvidenceMessage}".
        10. Không yêu cầu, suy đoán hoặc tự chọn subjectId. Phạm vi môn học đã được backend khóa theo phiên chat.

        Trả lời rõ ràng bằng ngôn ngữ của người dùng. Không mô tả nội bộ chain-of-thought; chỉ đưa kết luận và trích dẫn.
        """;

    private static string BuildAgenticUserPrompt(
        string question,
        IReadOnlyList<ChatHistoryEntry> history)
    {
        var historyBlock = history.Count == 0
            ? "(Không có lịch sử hội thoại)"
            : string.Join(
                Environment.NewLine,
                history.Select(entry => $"{entry.Role}: {entry.Content}"));

        return $"""
            Lịch sử hội thoại gần đây:
            {historyBlock}

            Câu hỏi hiện tại:
            {question}

            Hãy tự chọn và gọi các retrieval tool cần thiết trước khi đưa ra câu trả lời cuối cùng.
            """;
    }

    private static IReadOnlyList<AgentToolDefinition> CreateAgentTools(AgentToolSession session) =>
    [
        new(
            "search_documents",
            "Tìm tài liệu bằng hybrid retrieval: semantic pgvector + PostgreSQL full-text, sau đó hợp nhất bằng Reciprocal Rank Fusion. Dùng làm lựa chọn mặc định cho hầu hết câu hỏi.",
            new Func<string, CancellationToken, Task<string>>(session.SearchDocumentsAsync)),
        new(
            "keyword_search",
            "Tìm chính xác theo từ khóa/tên/mã/năm/thuật ngữ trong tài liệu của môn học hiện tại.",
            new Func<string, CancellationToken, Task<string>>(session.KeywordSearchAsync)),
        new(
            "get_chunk_context",
            "Lấy các đoạn ngay trước và sau một chunk đã tìm thấy để đọc đủ ngữ cảnh. Tham số là chunk_id do tool tìm kiếm trả về.",
            new Func<string, CancellationToken, Task<string>>(session.GetChunkContextAsync)),
        new(
            "list_documents",
            "Liệt kê tài liệu đã index trong môn học hiện tại. Truyền chuỗi rỗng để liệt kê chung hoặc truyền một phần tên tài liệu để lọc.",
            new Func<string, CancellationToken, Task<string>>(session.ListDocumentsAsync))
    ];

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

        foreach (var citation in citations)
        {
            _dbContext.MessageCitations.Add(new MessageCitation
            {
                Id = Guid.CreateVersion7(),
                ChatMessageId = message.Id,
                DocumentChunkId = citation.DocumentChunkId,
                Rank = citation.Rank
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
        ChatSession session,
        string firstQuestion,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(session.Title))
        {
            return;
        }

        var title = firstQuestion.Length > 50
            ? firstQuestion[..47] + "..."
            : firstQuestion;

        await _dbContext.ChatSessions
            .Where(s => s.Id == session.Id)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.Title, title)
                    .SetProperty(
                        x => x.UpdatedAtUtc,
                        DateTime.SpecifyKind(_clock.GetUtcNow().DateTime, DateTimeKind.Utc)),
                cancellationToken);
    }

    private IReadOnlyList<RagCitation> ParseCitationsFromAnswer(
        string answer,
        IReadOnlyList<RetrievedChunk> chunks)
    {
        if (string.IsNullOrEmpty(answer) || chunks.Count == 0)
        {
            return Array.Empty<RagCitation>();
        }

        var citationPattern = new System.Text.RegularExpressions.Regex(@"\[(\d+)\]");
        var matches = citationPattern.Matches(answer);

        if (matches.Count == 0)
        {
            return Array.Empty<RagCitation>();
        }

        var usedIndices = new HashSet<int>();
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            if (int.TryParse(match.Groups[1].Value, out var index)
                && index > 0
                && index <= chunks.Count)
            {
                usedIndices.Add(index);
            }
        }

        return usedIndices
            .OrderBy(index => index)
            .Select(index =>
            {
                var chunk = chunks[index - 1];
                return new RagCitation(
                    chunk.DocumentId,
                    chunk.DocumentChunkId,
                    chunk.DocumentTitle,
                    index,
                    TruncateExcerpt(chunk.Content, _options.Retrieval.ExcerptChars),
                    chunk.PageNumber,
                    chunk.SlideNumber);
            })
            .ToList();
    }

    private static string TruncateExcerpt(string content, int maxChars)
    {
        if (string.IsNullOrEmpty(content) || content.Length <= maxChars)
        {
            return content;
        }

        var truncated = content[..maxChars];
        var lastSpace = truncated.LastIndexOf(' ');
        if (lastSpace > maxChars / 2)
        {
            truncated = truncated[..lastSpace];
        }

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

    private sealed record PreparedGroundedQuery(
        IReadOnlyList<RetrievedChunk> Chunks,
        string? SystemPrompt,
        string? UserPrompt);

    private sealed class EvidenceCollector
    {
        private readonly int _maxToolResultChars;
        private readonly List<RetrievedChunk> _chunks = [];
        private readonly Dictionary<Guid, int> _indices = [];
        private int _metadataEvidenceCount;

        public EvidenceCollector(int maxToolResultChars)
        {
            _maxToolResultChars = Math.Max(1000, maxToolResultChars);
        }

        public int Count => _chunks.Count;
        public int TotalEvidenceCount => _chunks.Count + _metadataEvidenceCount;
        public bool HasEvidence => TotalEvidenceCount > 0;
        public bool RequiresCitations => _chunks.Count > 0;
        public IReadOnlyList<RetrievedChunk> Chunks => _chunks;

        public void AddDocumentMetadataEvidence(int count)
        {
            if (count > 0)
            {
                _metadataEvidenceCount += count;
            }
        }

        public string AddAndFormat(IReadOnlyList<RetrievedChunk> chunks)
        {
            if (chunks.Count == 0)
            {
                return "Không tìm thấy đoạn tài liệu phù hợp trong phạm vi môn học hiện tại.";
            }

            var builder = new StringBuilder();
            foreach (var chunk in chunks)
            {
                var citationIndex = Register(chunk);
                var location = chunk.PageNumber.HasValue
                    ? $"trang {chunk.PageNumber.Value}"
                    : chunk.SlideNumber.HasValue
                        ? $"slide {chunk.SlideNumber.Value}"
                        : "không rõ trang";

                var remaining = _maxToolResultChars - builder.Length;
                if (remaining <= 160)
                {
                    break;
                }

                var maxContent = Math.Min(1400, Math.Max(100, remaining - 150));
                var content = chunk.Content.Length <= maxContent
                    ? chunk.Content
                    : chunk.Content[..maxContent] + "...";

                builder.Append('[')
                    .Append(citationIndex)
                    .Append("] chunk_id=")
                    .Append(chunk.DocumentChunkId)
                    .Append(" | ")
                    .Append(chunk.DocumentTitle)
                    .Append(" | ")
                    .Append(location)
                    .AppendLine()
                    .AppendLine(content)
                    .AppendLine();
            }

            return builder.Length > 0
                ? builder.ToString()
                : "Không thể đưa thêm bằng chứng vì đã đạt giới hạn context của tool.";
        }

        private int Register(RetrievedChunk chunk)
        {
            if (_indices.TryGetValue(chunk.DocumentChunkId, out var existing))
            {
                return existing;
            }

            _chunks.Add(chunk);
            var index = _chunks.Count;
            _indices[chunk.DocumentChunkId] = index;
            return index;
        }
    }

    private sealed class AgentToolSession
    {
        private readonly IAgenticRetrievalService _retrieval;
        private readonly Guid _subjectId;
        private readonly EvidenceCollector _evidence;
        private readonly ConcurrentQueue<RagToolCallEvent> _events;
        private readonly int _topK;
        private int _callSequence;

        public AgentToolSession(
            IAgenticRetrievalService retrieval,
            Guid subjectId,
            EvidenceCollector evidence,
            ConcurrentQueue<RagToolCallEvent> events,
            int topK)
        {
            _retrieval = retrieval;
            _subjectId = subjectId;
            _evidence = evidence;
            _events = events;
            _topK = Math.Clamp(topK, 1, 12);
        }

        public async Task<string> SearchDocumentsAsync(
            string query,
            CancellationToken cancellationToken = default)
        {
            var id = NextCallId("hybrid");
            _events.Enqueue(new RagToolCallEvent(
                id,
                "search_documents",
                "running",
                $"Hybrid search: {query}",
                "pgvector + PostgreSQL full-text + Reciprocal Rank Fusion"));

            var chunks = await _retrieval.HybridSearchAsync(
                query,
                _subjectId,
                _topK,
                cancellationToken);
            var result = _evidence.AddAndFormat(chunks);

            _events.Enqueue(new RagToolCallEvent(
                id,
                "search_documents",
                "completed",
                $"Hybrid search tìm thấy {chunks.Count} đoạn",
                "Kết quả đã được đưa vào evidence context của agent."));

            return result;
        }

        public async Task<string> KeywordSearchAsync(
            string query,
            CancellationToken cancellationToken = default)
        {
            var id = NextCallId("keyword");
            _events.Enqueue(new RagToolCallEvent(
                id,
                "keyword_search",
                "running",
                $"Keyword search: {query}"));

            var chunks = await _retrieval.KeywordSearchAsync(
                query,
                _subjectId,
                _topK,
                cancellationToken);
            var result = _evidence.AddAndFormat(chunks);

            _events.Enqueue(new RagToolCallEvent(
                id,
                "keyword_search",
                "completed",
                $"Keyword search tìm thấy {chunks.Count} đoạn"));

            return result;
        }

        public async Task<string> GetChunkContextAsync(
            string chunkId,
            CancellationToken cancellationToken = default)
        {
            var id = NextCallId("context");
            _events.Enqueue(new RagToolCallEvent(
                id,
                "get_chunk_context",
                "running",
                "Đang lấy ngữ cảnh lân cận",
                $"chunk_id={chunkId}"));

            if (!Guid.TryParse(chunkId, out var parsedChunkId))
            {
                _events.Enqueue(new RagToolCallEvent(
                    id,
                    "get_chunk_context",
                    "completed",
                    "chunk_id không hợp lệ"));
                return "chunk_id không hợp lệ. Hãy dùng đúng chunk_id được search tool trả về.";
            }

            var chunks = await _retrieval.GetChunkContextAsync(
                parsedChunkId,
                _subjectId,
                before: 2,
                after: 2,
                cancellationToken);
            var result = _evidence.AddAndFormat(chunks);

            _events.Enqueue(new RagToolCallEvent(
                id,
                "get_chunk_context",
                "completed",
                $"Đã lấy {chunks.Count} đoạn ngữ cảnh lân cận"));

            return result;
        }

        public async Task<string> ListDocumentsAsync(
            string titleQuery,
            CancellationToken cancellationToken = default)
        {
            var id = NextCallId("documents");
            var normalizedQuery = string.IsNullOrWhiteSpace(titleQuery) ? null : titleQuery.Trim();

            _events.Enqueue(new RagToolCallEvent(
                id,
                "list_documents",
                "running",
                normalizedQuery is null
                    ? "Đang liệt kê tài liệu đã index"
                    : $"Đang tìm tài liệu có tên gần '{normalizedQuery}'"));

            var documents = await _retrieval.ListDocumentsAsync(
                _subjectId,
                normalizedQuery,
                cancellationToken: cancellationToken);

            _events.Enqueue(new RagToolCallEvent(
                id,
                "list_documents",
                "completed",
                $"Tìm thấy {documents.Count} tài liệu"));

            if (documents.Count == 0)
            {
                return "Không có tài liệu đã index phù hợp trong môn học hiện tại.";
            }

            _evidence.AddDocumentMetadataEvidence(documents.Count);

            return string.Join(
                Environment.NewLine,
                documents.Select(document =>
                    $"document_id={document.DocumentId} | {document.Title} | file={document.OriginalFileName}"));
        }

        private string NextCallId(string prefix) =>
            $"{prefix}-{Interlocked.Increment(ref _callSequence)}";
    }
}
