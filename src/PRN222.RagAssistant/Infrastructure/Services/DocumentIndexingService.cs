using Microsoft.EntityFrameworkCore;
using Pgvector;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Domain.Enums;
using PRN222.RagAssistant.Infrastructure.Parsing;

namespace PRN222.RagAssistant.Infrastructure.Services;

public sealed class DocumentIndexingService : IDocumentIndexingService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ITextEmbeddingService _embeddingService;
    private readonly DocumentParserFactory _parserFactory;
    private readonly TextChunker _textChunker;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DocumentIndexingService> _logger;

    public DocumentIndexingService(
        ApplicationDbContext dbContext,
        ITextEmbeddingService embeddingService,
        DocumentParserFactory parserFactory,
        TextChunker textChunker,
        IConfiguration configuration,
        ILogger<DocumentIndexingService> logger)
    {
        _dbContext = dbContext;
        _embeddingService = embeddingService;
        _parserFactory = parserFactory;
        _textChunker = textChunker;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task IndexAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var document = await _dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);

        if (document is null)
        {
            _logger.LogWarning("Document {DocumentId} not found, skipping indexing", documentId);
            return;
        }

        _logger.LogInformation("Starting indexing for document {DocumentId} ({Title})",
            documentId, document.Title);

        // Transition to Processing
        document.IndexStatus = DocumentIndexStatus.Processing;
        document.IndexError = null;
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            // 1. Resolve file path
            var uploadsPath = _configuration["Rag:Storage:UploadsPath"]
                ?? throw new InvalidOperationException("Rag:Storage:UploadsPath is not configured.");
            var filePath = Path.Combine(uploadsPath, document.StoragePath);

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Source file not found: {filePath}");
            }

            // 2. Parse document
            var parser = _parserFactory.GetParser(document.FileExtension);
            IReadOnlyList<ParsedPage> pages;

            await using (var fileStream = File.OpenRead(filePath))
            {
                pages = parser.Parse(fileStream);
            }

            if (pages.Count == 0)
            {
                throw new InvalidOperationException("Document parsing produced no content.");
            }

            _logger.LogInformation("Parsed {PageCount} pages from document {DocumentId}",
                pages.Count, documentId);

            // 3. Chunk
            var chunks = _textChunker.Chunk(pages);

            _logger.LogInformation("Created {ChunkCount} chunks from document {DocumentId}",
                chunks.Count, documentId);

            // 4. Embed each chunk
            var documentChunks = new List<DocumentChunk>();

            foreach (var chunk in chunks)
            {
                var embedding = await _embeddingService.EmbedAsync(chunk.Content, cancellationToken);

                documentChunks.Add(new DocumentChunk
                {
                    Id = Guid.NewGuid(),
                    DocumentId = documentId,
                    ChunkIndex = chunk.ChunkIndex,
                    Content = chunk.Content,
                    PageNumber = chunk.PageNumber,
                    SlideNumber = chunk.SlideNumber,
                    Embedding = new Vector(embedding)
                });
            }

            // 5. Replace existing chunks (coherent re-index)
            var existingChunks = await _dbContext.DocumentChunks
                .Where(c => c.DocumentId == documentId)
                .ToListAsync(cancellationToken);

            if (existingChunks.Count > 0)
            {
                _dbContext.DocumentChunks.RemoveRange(existingChunks);
                _logger.LogInformation("Removed {Count} existing chunks for document {DocumentId}",
                    existingChunks.Count, documentId);
            }

            // 6. Insert new chunks
            _dbContext.DocumentChunks.AddRange(documentChunks);

            // 7. Update document status
            document.IndexStatus = DocumentIndexStatus.Indexed;
            document.IndexedAtUtc = DateTime.UtcNow;
            document.IndexError = null;

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Successfully indexed document {DocumentId} with {ChunkCount} chunks",
                documentId, documentChunks.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to index document {DocumentId}", documentId);

            document.IndexStatus = DocumentIndexStatus.Failed;
            document.IndexError = ex.Message.Length > 2000
                ? ex.Message[..2000]
                : ex.Message;

            await _dbContext.SaveChangesAsync(CancellationToken.None);
        }
    }
}
