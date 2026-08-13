using PRN222.RagAssistant.Application.Abstractions;

namespace PRN222.RagAssistant.Infrastructure.Services;

public sealed class DocumentIndexingWorker : BackgroundService
{
    private readonly IDocumentIndexingQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DocumentIndexingWorker> _logger;

    public DocumentIndexingWorker(
        IDocumentIndexingQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<DocumentIndexingWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Document indexing worker started");

        await RehydratePendingDocumentsAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var documentId = await _queue.DequeueAsync(stoppingToken);

                _logger.LogInformation("Dequeued document {DocumentId} for indexing", documentId);

                await using var scope = _scopeFactory.CreateAsyncScope();
                var indexingService = scope.ServiceProvider.GetRequiredService<IDocumentIndexingService>();

                await indexingService.IndexAsync(documentId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Graceful shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in document indexing worker");

                // Brief delay to avoid tight error loops
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("Document indexing worker stopped");
    }

    private async Task RehydratePendingDocumentsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<Data.ApplicationDbContext>();

            var pendingDocumentIds = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                dbContext.Documents
                    .Where(d => d.IndexStatus == Domain.Enums.DocumentIndexStatus.Uploaded ||
                                d.IndexStatus == Domain.Enums.DocumentIndexStatus.Processing)
                    .Select(d => d.Id),
                cancellationToken);

            if (pendingDocumentIds.Count > 0)
            {
                _logger.LogInformation("Rehydrating {Count} pending document(s) for indexing", pendingDocumentIds.Count);

                foreach (var docId in pendingDocumentIds)
                {
                    await _queue.EnqueueAsync(docId, cancellationToken);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to rehydrate pending documents on startup");
        }
    }
}
