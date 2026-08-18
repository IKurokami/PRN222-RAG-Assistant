using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector;
using PRN222.RagAssistant.Infrastructure.Rag;
using PRN222.RagAssistant.Data;

namespace PRN222.RagAssistant.Infrastructure.Rag;

/// <summary>
/// Retrieves relevant document chunks from pgvector based on cosine similarity using raw SQL.
/// </summary>
public sealed class PgVectorDocumentChunkRetriever : IDocumentChunkRetriever
{
    private readonly ApplicationDbContext _dbContext;
    private readonly RagOptions _options;
    private readonly ILogger<PgVectorDocumentChunkRetriever> _logger;

    public PgVectorDocumentChunkRetriever(
        ApplicationDbContext dbContext,
        Microsoft.Extensions.Options.IOptions<RagOptions> options,
        ILogger<PgVectorDocumentChunkRetriever> logger)
    {
        _dbContext = dbContext;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        float[] questionEmbedding,
        Guid subjectId,
        CancellationToken cancellationToken = default)
    {
        var topK = _options.Retrieval.TopK;

        var embeddingString = "[" + string.Join(",", questionEmbedding.Select(x => x.ToString(System.Globalization.CultureInfo.InvariantCulture))) + "]";

        var sql = $@"
            SELECT dc.""Id"", dc.""DocumentId"", d.""Title"", dc.""Content"",
                   dc.""PageNumber"", dc.""SlideNumber"",
                   dc.""Embedding"" <=> @embedding AS distance,
                   dc.""ChunkIndex""
            FROM ""DocumentChunks"" dc
            JOIN ""Documents"" d ON d.""Id"" = dc.""DocumentId""
            WHERE d.""SubjectId"" = @subjectId
              AND d.""IndexStatus"" = 'Indexed'
              AND dc.""Embedding"" IS NOT NULL
            ORDER BY dc.""Embedding"" <=> @embedding
            LIMIT {topK}";

        var results = await _dbContext.Database
            .SqlQueryRaw<ResultRow>(sql,
                new NpgsqlParameter("@embedding", embeddingString),
                new NpgsqlParameter("@subjectId", subjectId))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var chunks = results
            .Select(r => new RetrievedChunk(
                DocumentChunkId: r.Id,
                DocumentId: r.DocumentId,
                DocumentTitle: r.Title,
                Content: r.Content,
                PageNumber: r.PageNumber,
                SlideNumber: r.SlideNumber,
                SimilarityScore: 1.0 - r.Distance))
            .ToList();

        _logger.LogDebug(
            "Retrieved {Count} chunks for query (TopK={TopK}, SubjectId={SubjectId})",
            chunks.Count, topK, subjectId);

        return chunks;
    }

    private sealed class ResultRow
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public int? PageNumber { get; set; }
        public int? SlideNumber { get; set; }
        public double Distance { get; set; }
        public int ChunkIndex { get; set; }
    }
}
