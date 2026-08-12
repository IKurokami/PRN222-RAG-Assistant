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
}
