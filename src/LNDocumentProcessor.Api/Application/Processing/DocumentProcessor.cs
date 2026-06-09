using System.Text;
using LNDocumentProcessor.Api.Application.Abstractions;
using LNDocumentProcessor.Api.Domain;

namespace LNDocumentProcessor.Api.Application.Processing;

/// <summary>
/// Processes a single queued message: reads the stored content, generates a
/// preview, stores it, and advances the document to Processed. Failures are
/// recorded as a Failed status with a reason so they are visible via the status
/// and audit endpoints. This type is the unit-testable core; the hosted worker
/// just feeds it messages.
/// </summary>
public sealed class DocumentProcessor
{
    private readonly IDocumentRepository _repository;
    private readonly IStorageService _storage;
    private readonly IPreviewGenerator _previewGenerator;
    private readonly IStatusNotifier _statusNotifier;
    private readonly TimeProvider _clock;
    private readonly ILogger<DocumentProcessor> _logger;

    public DocumentProcessor(
        IDocumentRepository repository,
        IStorageService storage,
        IPreviewGenerator previewGenerator,
        IStatusNotifier statusNotifier,
        TimeProvider clock,
        ILogger<DocumentProcessor> logger)
    {
        _repository = repository;
        _storage = storage;
        _previewGenerator = previewGenerator;
        _statusNotifier = statusNotifier;
        _clock = clock;
        _logger = logger;
    }

    public async Task ProcessAsync(DocumentProcessingMessage message, CancellationToken cancellationToken = default)
    {
        var document = await _repository.GetByIdAsync(message.DocumentId, cancellationToken);
        if (document is null)
        {
            _logger.LogWarning("Processing message for unknown document {DocumentId}; skipping.", message.DocumentId);
            return;
        }

        if (document.BlobReference is null)
        {
            await FailAsync(document, "Document has no stored content to process.", cancellationToken);
            return;
        }

        try
        {
            document.MarkProcessing(_clock.GetUtcNow());
            await _repository.UpdateAsync(document, cancellationToken);

            await using var content = await _storage.OpenReadAsync(document.BlobReference, cancellationToken)
                ?? throw new InvalidOperationException($"Stored content '{document.BlobReference}' could not be opened.");

            var previewText = await _previewGenerator.GenerateAsync(
                content, document.Metadata.ContentType, cancellationToken);

            var previewBytes = Encoding.UTF8.GetBytes(previewText);
            var previewKey = $"{document.Provider}/{document.Id}/preview.txt";
            using var previewStream = new MemoryStream(previewBytes);
            var stored = await _storage.SaveAsync(previewKey, previewStream, "text/plain; charset=utf-8", cancellationToken);

            document.MarkProcessed(stored.Reference, stored.SizeBytes, _clock.GetUtcNow());
            await _repository.UpdateAsync(document, cancellationToken);

            _logger.LogInformation(
                "Processed document {DocumentId}; preview is {Bytes} bytes.", document.Id, stored.SizeBytes);

            await _statusNotifier.NotifyAsync(
                document.Id, document.SourceDocumentId, document.Status, document.PreviewSizeBytes, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await FailAsync(document, ex.Message, cancellationToken);
        }
    }

    private async Task FailAsync(Document document, string reason, CancellationToken cancellationToken)
    {
        document.MarkFailed(reason, _clock.GetUtcNow());
        await _repository.UpdateAsync(document, cancellationToken);
        _logger.LogError("Failed to process document {DocumentId}: {Reason}", document.Id, reason);

        await _statusNotifier.NotifyAsync(
            document.Id, document.SourceDocumentId, document.Status, document.PreviewSizeBytes, cancellationToken);
    }
}
