using LNDocumentProcessor.Api.Application.Abstractions;
using LNDocumentProcessor.Api.Domain;

namespace LNDocumentProcessor.Api.Infrastructure.Notifications;

/// <summary>
/// Local stub for the optional "emit a status update externally" requirement.
/// Logs the status change; a real implementation would publish to a topic or
/// POST to a webhook. Behind <see cref="IStatusNotifier"/> so it can be swapped
/// without touching the processor.
/// </summary>
public sealed class LoggingStatusNotifier : IStatusNotifier
{
    private readonly ILogger<LoggingStatusNotifier> _logger;

    public LoggingStatusNotifier(ILogger<LoggingStatusNotifier> logger) => _logger = logger;

    public Task NotifyAsync(
        Guid documentId,
        string sourceDocumentId,
        DocumentStatus status,
        long? previewSizeBytes,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "STATUS EVENT → documentId={DocumentId} sourceDocumentId={SourceDocumentId} status={Status} previewSizeBytes={PreviewSizeBytes}",
            documentId, sourceDocumentId, status, previewSizeBytes);
        return Task.CompletedTask;
    }
}
