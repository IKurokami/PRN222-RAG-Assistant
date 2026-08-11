using Pgvector;

namespace PRN222.RagAssistant.Domain.Entities;

public sealed class DocumentChunk
{
    public Guid Id { get; set; }

    public Guid DocumentId { get; set; }

    public int ChunkIndex { get; set; }

    public string Content { get; set; } = string.Empty;

    public int? PageNumber { get; set; }

    public int? SlideNumber { get; set; }

    public Vector? Embedding { get; set; }
}
