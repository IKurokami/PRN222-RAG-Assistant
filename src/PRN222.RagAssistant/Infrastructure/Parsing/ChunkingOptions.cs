namespace PRN222.RagAssistant.Infrastructure.Parsing;

/// <summary>
/// Configuration options for document chunking behavior.
/// </summary>
public sealed class ChunkingOptions
{
    public const string SectionName = "Chunking";

    /// <summary>
    /// Maximum number of characters per chunk.
    /// </summary>
    public int MaxChunkSize { get; set; } = 1000;

    /// <summary>
    /// Number of characters to overlap between adjacent chunks.
    /// </summary>
    public int OverlapSize { get; set; } = 0;
}


