using LNDocumentProcessor.Api.Application.Abstractions;
using LNDocumentProcessor.Api.Application.Processing;

namespace LNDocumentProcessor.Api.Worker;

/// <summary>
/// In-process background worker that drains the processing queue and hands each
/// message to a <see cref="DocumentProcessor"/>. A scope is created per message
/// so the processor (and any scoped dependencies it later gains) behaves like a
/// real per-message consumer. One processing failure never stops the loop.
/// </summary>
public sealed class DocumentProcessingWorker : BackgroundService
{
    private readonly IDocumentProcessingQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DocumentProcessingWorker> _logger;

    public DocumentProcessingWorker(
        IDocumentProcessingQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<DocumentProcessingWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Document processing worker started.");

        await foreach (var message in _queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<DocumentProcessor>();
                await processor.ProcessAsync(message, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // DocumentProcessor records per-document failures itself; this guards
                // against infrastructure faults (e.g. scope creation) so the loop survives.
                _logger.LogError(ex, "Unhandled error processing message for document {DocumentId}.", message.DocumentId);
            }
        }

        _logger.LogInformation("Document processing worker stopping.");
    }
}
