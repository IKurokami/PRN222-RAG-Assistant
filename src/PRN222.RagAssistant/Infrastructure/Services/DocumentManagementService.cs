using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Domain.Enums;

namespace PRN222.RagAssistant.Infrastructure.Services;

public sealed class DocumentManagementService(
    ApplicationDbContext dbContext,
    IDocumentIndexingQueue indexingQueue,
    IConfiguration configuration,
    ILogger<DocumentManagementService> logger,
    IManagementRealtimeNotifier realtimeNotifier) : IDocumentManagementService
{
    public async Task<IReadOnlyList<Document>> GetDocumentsAsync(
        Guid subjectId,
        Guid? selectedChapterId = null,
        string? searchTerm = null,
        DocumentIndexStatus? selectedStatus = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Documents
            .AsNoTracking()
            .Where(document => document.SubjectId == subjectId);

        if (selectedChapterId.HasValue)
        {
            query = query.Where(document => document.ChapterId == selectedChapterId.Value);
        }

        var normalizedSearchTerm = searchTerm?.Trim();
        if (!string.IsNullOrEmpty(normalizedSearchTerm))
        {
            var normalizedSearch = normalizedSearchTerm.ToLowerInvariant();
            query = query.Where(document =>
                document.Title.ToLower().Contains(normalizedSearch)
                || document.OriginalFileName.ToLower().Contains(normalizedSearch));
        }

        if (selectedStatus.HasValue)
        {
            query = query.Where(document => document.IndexStatus == selectedStatus.Value);
        }

        return await query
            .OrderByDescending(document => document.UploadedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public Task<int> GetDocumentCountAsync(
        Guid subjectId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Documents
            .AsNoTracking()
            .CountAsync(document => document.SubjectId == subjectId, cancellationToken);
    }

    public Task<Document?> GetDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(document => document.Id == documentId, cancellationToken);
    }

    public async Task<DocumentChunkPreviewData> GetChunkPreviewAsync(
        Guid documentId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (pageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        }

        var chunkQuery = dbContext.DocumentChunks
            .AsNoTracking()
            .Where(chunk => chunk.DocumentId == documentId);

        var totalCount = await chunkQuery.CountAsync(cancellationToken);
        var embeddedCount = totalCount == 0
            ? 0
            : await chunkQuery.CountAsync(chunk => chunk.Embedding != null, cancellationToken);
        var totalPages = totalCount == 0 ? 0 : ((totalCount - 1) / pageSize) + 1;
        var currentPage = totalPages == 0 ? 1 : Math.Clamp(page, 1, totalPages);

        List<DocumentChunkPreviewItemData> items = totalCount == 0
            ? []
            : await chunkQuery
                .OrderBy(chunk => chunk.ChunkIndex)
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .Select(chunk => new DocumentChunkPreviewItemData(
                    chunk.ChunkIndex,
                    chunk.Content,
                    chunk.PageNumber,
                    chunk.SlideNumber,
                    chunk.Embedding != null))
                .ToListAsync(cancellationToken);

        return new DocumentChunkPreviewData(
            items,
            totalCount,
            embeddedCount,
            currentPage,
            totalPages);
    }

    public async Task<Document> CreateDocumentAsync(
        DocumentCreateRequest request,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        if (request.ChapterId.HasValue)
        {
            var chapterValid = await dbContext.Chapters.AnyAsync(
                chapter => chapter.Id == request.ChapterId.Value
                    && chapter.SubjectId == request.SubjectId,
                cancellationToken);

            if (!chapterValid)
            {
                throw new InvalidOperationException(
                    "The selected chapter is invalid or does not belong to the requested subject.");
            }
        }

        var uploadsFolderSetting = configuration["Rag:Storage:UploadsPath"] ?? "storage/uploads";
        var uploadsFolder = Path.IsPathRooted(uploadsFolderSetting)
            ? uploadsFolderSetting
            : Path.Combine(Directory.GetCurrentDirectory(), uploadsFolderSetting);

        Directory.CreateDirectory(uploadsFolder);

        var documentId = Guid.NewGuid();
        var extension = request.FileExtension.ToLowerInvariant();
        var storagePath = Path.Combine(uploadsFolder, $"{documentId}{extension}").Replace('\\', '/');

        await using (var fileStream = new FileStream(
            storagePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None))
        {
            await content.CopyToAsync(fileStream, cancellationToken);
        }

        var document = new Document
        {
            Id = documentId,
            SubjectId = request.SubjectId,
            ChapterId = request.ChapterId,
            UploadedByUserId = request.UploadedByUserId,
            Title = request.Title,
            OriginalFileName = request.OriginalFileName,
            StoragePath = storagePath,
            ContentType = request.ContentType,
            FileExtension = extension,
            FileSizeBytes = request.FileSizeBytes,
            IndexStatus = DocumentIndexStatus.Uploaded,
            UploadedAtUtc = DateTime.UtcNow
        };

        try
        {
            dbContext.Documents.Add(document);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to persist document record for {DocumentId}. Cleaning up uploaded file at {StoragePath}.",
                documentId,
                storagePath);

            TryDeleteFile(storagePath);
            throw;
        }
        await realtimeNotifier.PublishAsync(
            new ManagementRealtimeEvent(
                ManagementResource.Document,
                ManagementChange.Created,
                document.Id,
                document.SubjectId),
            CancellationToken.None);

        await indexingQueue.EnqueueAsync(document.Id, cancellationToken);
        return document;
    }

    public async Task<Document?> UpdateDocumentAsync(
        Guid documentId,
        string title,
        Guid? chapterId,
        CancellationToken cancellationToken = default)
    {
        var document = await dbContext.Documents
            .FirstOrDefaultAsync(candidate => candidate.Id == documentId, cancellationToken);

        if (document is null)
        {
            return null;
        }

        if (chapterId.HasValue)
        {
            var chapterValid = await dbContext.Chapters.AnyAsync(
                chapter => chapter.Id == chapterId.Value
                    && chapter.SubjectId == document.SubjectId,
                cancellationToken);

            if (!chapterValid)
            {
                throw new InvalidOperationException(
                    "The selected chapter is invalid or does not belong to the document subject.");
            }
        }

        document.Title = title;
        document.ChapterId = chapterId;
        await dbContext.SaveChangesAsync(cancellationToken);
        await realtimeNotifier.PublishAsync(
            new ManagementRealtimeEvent(
                ManagementResource.Document,
                ManagementChange.Updated,
                document.Id,
                document.SubjectId),
            CancellationToken.None);
        return document;
    }

    public async Task<Document?> RequeueForIndexAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var document = await dbContext.Documents
            .FirstOrDefaultAsync(candidate => candidate.Id == documentId, cancellationToken);

        if (document is null)
        {
            return null;
        }

        document.IndexStatus = DocumentIndexStatus.Uploaded;
        document.IndexError = null;
        document.IndexedAtUtc = null;

        await dbContext.SaveChangesAsync(cancellationToken);
        await realtimeNotifier.PublishAsync(
            new ManagementRealtimeEvent(
                ManagementResource.Document,
                ManagementChange.IndexStatusChanged,
                document.Id,
                document.SubjectId,
                document.IndexStatus.ToString()),
            CancellationToken.None);
        await indexingQueue.EnqueueAsync(document.Id, cancellationToken);
        return document;
    }

    public async Task<DocumentDeleteResult?> DeleteDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var document = await dbContext.Documents
            .FirstOrDefaultAsync(candidate => candidate.Id == documentId, cancellationToken);

        if (document is null)
        {
            return null;
        }

        var result = new DocumentDeleteResult(
            document.Id,
            document.SubjectId,
            document.Title);
        var storagePath = document.StoragePath;

        dbContext.Documents.Remove(document);
        await dbContext.SaveChangesAsync(cancellationToken);
        await realtimeNotifier.PublishAsync(
            new ManagementRealtimeEvent(
                ManagementResource.Document,
                ManagementChange.Deleted,
                document.Id,
                document.SubjectId),
            CancellationToken.None);

        TryDeleteFile(storagePath);
        return result;
    }

    private void TryDeleteFile(string storagePath)
    {
        if (!File.Exists(storagePath))
        {
            return;
        }

        try
        {
            File.Delete(storagePath);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Document database state changed but failed to remove physical file at {StoragePath}.",
                storagePath);
        }
    }
}
