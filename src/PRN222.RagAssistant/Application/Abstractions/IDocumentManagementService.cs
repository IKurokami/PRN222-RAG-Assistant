using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Domain.Enums;

namespace PRN222.RagAssistant.Application.Abstractions;

public interface IDocumentManagementService
{
    Task<IReadOnlyList<Document>> GetDocumentsAsync(
        Guid subjectId,
        Guid? selectedChapterId = null,
        string? searchTerm = null,
        DocumentIndexStatus? selectedStatus = null,
        CancellationToken cancellationToken = default);

    Task<int> GetDocumentCountAsync(
        Guid subjectId,
        CancellationToken cancellationToken = default);

    Task<Document?> GetDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);

    Task<DocumentChunkPreviewData> GetChunkPreviewAsync(
        Guid documentId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Document> CreateDocumentAsync(
        DocumentCreateRequest request,
        Stream content,
        CancellationToken cancellationToken = default);

    Task<Document?> UpdateDocumentAsync(
        Guid documentId,
        string title,
        Guid? chapterId,
        CancellationToken cancellationToken = default);

    Task<Document?> RequeueForIndexAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);

    Task<DocumentDeleteResult?> DeleteDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);
}

public sealed record DocumentCreateRequest(
    Guid SubjectId,
    Guid? ChapterId,
    Guid UploadedByUserId,
    string Title,
    string OriginalFileName,
    string ContentType,
    string FileExtension,
    long FileSizeBytes);

public sealed record DocumentDeleteResult(
    Guid Id,
    Guid SubjectId,
    string Title);

public sealed record DocumentChunkPreviewData(
    IReadOnlyList<DocumentChunkPreviewItemData> Items,
    int TotalCount,
    int EmbeddedCount,
    int CurrentPage,
    int TotalPages);

public sealed record DocumentChunkPreviewItemData(
    int ChunkIndex,
    string Content,
    int? PageNumber,
    int? SlideNumber,
    bool HasEmbedding);
