using LNDocumentProcessor.Api.Application.Processing;

namespace LNDocumentProcessor.Api.Application.Abstractions;

/// <summary>
/// Queue port for background processing work. The local implementation is an
/// in-memory channel; a cloud implementation (Azure Service Bus / AWS SQS)
/// implements the same contract — producers enqueue, the worker drains via
/// <see cref="DequeueAllAsync"/>.
/// </summary>
public interface IDocumentProcessingQueue
{
    /// <summary>Enqueues a message for background processing.</summary>
    ValueTask EnqueueAsync(DocumentProcessingMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously yields messages as they become available, completing when
    /// the queue is closed or the token is cancelled. Intended for the worker's
    /// receive loop.
    /// </summary>
    IAsyncEnumerable<DocumentProcessingMessage> DequeueAllAsync(CancellationToken cancellationToken = default);
}
