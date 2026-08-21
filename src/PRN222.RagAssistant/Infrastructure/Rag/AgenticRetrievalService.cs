using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Enums;

namespace PRN222.RagAssistant.Infrastructure.Rag;

public interface IAgenticRetrievalService
{
    Task<IReadOnlyList<RetrievedChunk>> HybridSearchAsync(
        string query,
        Guid subjectId,
        int topK,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RetrievedChunk>> KeywordSearchAsync(
        string query,
        Guid subjectId,
        int topK,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RetrievedChunk>> GetChunkContextAsync(
        Guid chunkId,
        Guid subjectId,
        int before,
        int after,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AgentDocumentInfo>> ListDocumentsAsync(
        Guid subjectId,
        string? titleQuery = null,
        int limit = 20,
        CancellationToken cancellationToken = default);
}

public sealed record AgentDocumentInfo(
    Guid DocumentId,
    string Title,
    string OriginalFileName,
    DateTime? IndexedAtUtc);

/// <summary>
/// Server-side retrieval toolbox for agentic RAG. The subject scope is always supplied
/// by RagQueryService from the authorized ChatSession; it is intentionally not exposed
/// as a model-controlled tool argument.
/// </summary>
public sealed class AgenticRetrievalService : IAgenticRetrievalService
{
    private const double RrfK = 60.0;

    private readonly ApplicationDbContext _dbContext;
    private readonly ITextEmbeddingService _embeddingService;
    private readonly IDocumentChunkRetriever _vectorRetriever;

    public AgenticRetrievalService(
        ApplicationDbContext dbContext,
        ITextEmbeddingService embeddingService,
        IDocumentChunkRetriever vectorRetriever)
    {
        _dbContext = dbContext;
        _embeddingService = embeddingService;
        _vectorRetriever = vectorRetriever;
    }

    public async Task<IReadOnlyList<RetrievedChunk>> HybridSearchAsync(
        string query,
        Guid subjectId,
        int topK,
        CancellationToken cancellationToken = default)
    {
        ValidateQuery(query);
        topK = Math.Clamp(topK, 1, 12);

        var embedding = await _embeddingService.EmbedAsync(query, cancellationToken);
        var semantic = await _vectorRetriever.SearchAsync(embedding, subjectId, cancellationToken);
        var keyword = await KeywordSearchAsync(query, subjectId, Math.Max(topK, 8), cancellationToken);

        var fused = new Dictionary<Guid, (RetrievedChunk Chunk, double Score)>();
        AddRrfScores(fused, semantic, topK * 2);
        AddRrfScores(fused, keyword, topK * 2);

        return fused.Values
            .OrderByDescending(item => item.Score)
            .Take(topK)
            .Select(item => item.Chunk with { SimilarityScore = item.Score })
            .ToList();
    }

    public async Task<IReadOnlyList<RetrievedChunk>> KeywordSearchAsync(
        string query,
        Guid subjectId,
        int topK,
        CancellationToken cancellationToken = default)
    {
        ValidateQuery(query);
        topK = Math.Clamp(topK, 1, 20);

        const string sql = """
            SELECT
                dc."Id",
                dc."DocumentId",
                d."Title",
                dc."Content",
                dc."PageNumber",
                dc."SlideNumber",
                ts_rank_cd(
                    to_tsvector('simple', coalesce(d."Title", '') || ' ' || dc."Content"),
                    websearch_to_tsquery('simple', {0})) AS "Rank"
            FROM "DocumentChunks" dc
            JOIN "Documents" d ON d."Id" = dc."DocumentId"
            WHERE d."IndexStatus" = 'Indexed'
              AND d."SubjectId" = {1}
              AND to_tsvector('simple', coalesce(d."Title", '') || ' ' || dc."Content")
                  @@ websearch_to_tsquery('simple', {0})
            ORDER BY "Rank" DESC, dc."ChunkIndex" ASC
            LIMIT {2}
            """;

        var rows = await _dbContext.Database
            .SqlQueryRaw<KeywordResultRow>(sql, query, subjectId, topK)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rows.Select(row => new RetrievedChunk(
                row.Id,
                row.DocumentId,
                row.Title,
                row.Content,
                row.PageNumber,
                row.SlideNumber,
                row.Rank))
            .ToList();
    }

    public async Task<IReadOnlyList<RetrievedChunk>> GetChunkContextAsync(
        Guid chunkId,
        Guid subjectId,
        int before,
        int after,
        CancellationToken cancellationToken = default)
    {
        before = Math.Clamp(before, 0, 5);
        after = Math.Clamp(after, 0, 5);

        var target = await (
                from chunk in _dbContext.DocumentChunks.AsNoTracking()
                join document in _dbContext.Documents.AsNoTracking()
                    on chunk.DocumentId equals document.Id
                where chunk.Id == chunkId
                      && document.SubjectId == subjectId
                      && document.IndexStatus == DocumentIndexStatus.Indexed
                select new
                {
                    chunk.DocumentId,
                    chunk.ChunkIndex,
                    document.Title
                })
            .SingleOrDefaultAsync(cancellationToken);

        if (target is null)
        {
            return Array.Empty<RetrievedChunk>();
        }

        var minIndex = Math.Max(0, target.ChunkIndex - before);
        var maxIndex = target.ChunkIndex + after;

        var neighbors = await _dbContext.DocumentChunks
            .AsNoTracking()
            .Where(chunk => chunk.DocumentId == target.DocumentId
                            && chunk.ChunkIndex >= minIndex
                            && chunk.ChunkIndex <= maxIndex)
            .OrderBy(chunk => chunk.ChunkIndex)
            .Select(chunk => new
            {
                chunk.Id,
                chunk.DocumentId,
                chunk.Content,
                chunk.PageNumber,
                chunk.SlideNumber
            })
            .ToListAsync(cancellationToken);

        return neighbors.Select(chunk => new RetrievedChunk(
                chunk.Id,
                chunk.DocumentId,
                target.Title,
                chunk.Content,
                chunk.PageNumber,
                chunk.SlideNumber,
                1.0))
            .ToList();
    }

    public async Task<IReadOnlyList<AgentDocumentInfo>> ListDocumentsAsync(
        Guid subjectId,
        string? titleQuery = null,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 50);

        var query = _dbContext.Documents
            .AsNoTracking()
            .Where(document => document.SubjectId == subjectId
                               && document.IndexStatus == DocumentIndexStatus.Indexed);

        if (!string.IsNullOrWhiteSpace(titleQuery))
        {
            var pattern = $"%{titleQuery.Trim()}%";
            query = query.Where(document => EF.Functions.ILike(document.Title, pattern)
                                            || EF.Functions.ILike(document.OriginalFileName, pattern));
        }

        return await query
            .OrderBy(document => document.Title)
            .Take(limit)
            .Select(document => new AgentDocumentInfo(
                document.Id,
                document.Title,
                document.OriginalFileName,
                document.IndexedAtUtc))
            .ToListAsync(cancellationToken);
    }

    private static void AddRrfScores(
        IDictionary<Guid, (RetrievedChunk Chunk, double Score)> fused,
        IReadOnlyList<RetrievedChunk> chunks,
        int maxItems)
    {
        for (var index = 0; index < Math.Min(chunks.Count, maxItems); index++)
        {
            var chunk = chunks[index];
            var contribution = 1.0 / (RrfK + index + 1);

            if (fused.TryGetValue(chunk.DocumentChunkId, out var existing))
            {
                fused[chunk.DocumentChunkId] = (existing.Chunk, existing.Score + contribution);
            }
            else
            {
                fused[chunk.DocumentChunkId] = (chunk, contribution);
            }
        }
    }

    private static void ValidateQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Search query must not be empty.", nameof(query));
        }
    }

    private sealed class KeywordResultRow
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public int? PageNumber { get; set; }
        public int? SlideNumber { get; set; }
        public double Rank { get; set; }
    }
}
