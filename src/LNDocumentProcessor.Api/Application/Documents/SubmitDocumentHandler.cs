using LNDocumentProcessor.Api.Application.Abstractions;
using LNDocumentProcessor.Api.Application.Processing;
using LNDocumentProcessor.Api.Domain;

namespace LNDocumentProcessor.Api.Application.Documents;

/// <summary>
/// The core Task 1 use case: receive a submission, deduplicate, store raw
/// content, and persist metadata with an audit trail.
///
/// Idempotency: the dedup check runs before any side effect, so re-submitting
/// the same external document (provider + sourceDocumentId) returns the
/// existing record without writing a second copy or creating a second record.
/// </summary>
public sealed class SubmitDocumentHandler
{
    private readonly IDocumentRepository _repository;
    private readonly IStorageService _storage;
    private readonly IDocumentProcessingQueue _queue;
    private readonly TimeProvider _clock;
    private readonly ILogger<SubmitDocumentHandler> _logger;

    public SubmitDocumentHandler(
        IDocumentRepository repository,
        IStorageService storage,
        IDocumentProcessingQueue queue,
        TimeProvider clock,
        ILogger<SubmitDocumentHandler> logger)
    {
        _repository = repository;
        _storage = storage;
        _queue = queue;
        _clock = clock;
        _logger = logger;
    }

    public async Task<SubmitDocumentResult> HandleAsync(
        SubmitDocumentCommand command,
        CancellationToken cancellationToken = default)
    {
        // 1. Deduplicate before any side effect.
        var existing = await _repository.GetByDedupKeyAsync(
            command.Provider, command.SourceDocumentId, cancellationToken);

        if (existing is not null)
        {
            _logger.LogInformation(
                "Duplicate submission for {DedupKey}; returning existing document {DocumentId}.",
                Document.DedupKey(command.Provider, command.SourceDocumentId), existing.Id);
            return new SubmitDocumentResult(existing, IsDuplicate: true);
        }

        // 2. Assign internal id; record Received.
        var now = _clock.GetUtcNow();
        var documentId = Guid.NewGuid();
        var metadata = new DocumentMetadata(
            command.Title,
            command.Jurisdiction,
            command.Categories,
            command.Tags,
            command.ContentType,
            command.FileName);

        var document = Document.Receive(
            documentId, command.Provider, command.SourceDocumentId, metadata, now);

        // 3. Store raw content, then record Stored.
        var objectKey = BuildObjectKey(document);
        var stored = await _storage.SaveAsync(
            objectKey, command.Content, command.ContentType, cancellationToken);

        document.MarkStored(stored.Reference, stored.SizeBytes, _clock.GetUtcNow());

        // 4. Persist metadata + audit trail.
        await _repository.AddAsync(document, cancellationToken);

        // 5. Mark Queued BEFORE enqueuing so the worker never observes a
        //    document still mid-intake (the in-memory repo shares the instance).
        document.MarkQueued(_clock.GetUtcNow());
        await _repository.UpdateAsync(document, cancellationToken);

        var message = new DocumentProcessingMessage(
            document.Id,
            document.SourceDocumentId,
            DocumentProcessingMessage.GeneratePreviewAction,
            _clock.GetUtcNow());
        await _queue.EnqueueAsync(message, cancellationToken);

        _logger.LogInformation(
            "Stored and queued document {DocumentId} ({SizeBytes} bytes) for {DedupKey}.",
            document.Id, stored.SizeBytes, document.DedupKeyValue);

        return new SubmitDocumentResult(document, IsDuplicate: false);
    }

    /// <summary>
    /// Storage key layout: partition by provider, then internal id, preserving
    /// the original file name. Maps cleanly onto blob/object key conventions.
    /// </summary>
    private static string BuildObjectKey(Document document)
        => $"{document.Provider}/{document.Id}/{document.Metadata.FileName}";
}
