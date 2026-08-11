namespace PRN222.RagAssistant.Application.Models;

/// <summary>
/// Source metadata returned with a grounded answer for rendering and traceability.
/// </summary>
public sealed record RagCitation(
    Guid DocumentId,
    Guid DocumentChunkId,
    string DocumentTitle,
    int Rank,
    string Excerpt,
    int? PageNumber,
    int? SlideNumber);
