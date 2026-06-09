using LNDocumentProcessor.Api.Domain;

namespace LNDocumentProcessor.Api.Application.Abstractions;

/// <summary>
/// Emits a status update to an external channel (queue/topic/webhook). The
/// default implementation is a local logging stub; a real implementation would
/// publish to a notifications topic or call a webhook. This is the optional
/// "emit a status update externally" extension point.
/// </summary>
public interface IStatusNotifier
{
    Task NotifyAsync(
        Guid documentId,
        string sourceDocumentId,
        DocumentStatus status,
        long? previewSizeBytes,
        CancellationToken cancellationToken = default);
}
