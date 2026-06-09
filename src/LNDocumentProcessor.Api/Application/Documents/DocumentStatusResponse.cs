using LNDocumentProcessor.Api.Domain;

namespace LNDocumentProcessor.Api.Application.Documents;

/// <summary>
/// Lightweight processing-status projection: the fields the assignment lists for
/// status information (documentId, sourceDocumentId, status, timestamp, preview
/// size indicator), plus a failure reason when the document is Failed.
/// </summary>
public sealed record DocumentStatusResponse(
    Guid DocumentId,
    string SourceDocumentId,
    string Status,
    DateTimeOffset Timestamp,
    long? PreviewSizeBytes,
    string? FailureReason)
{
    public static DocumentStatusResponse FromDomain(Document d)
    {
        var last = d.AuditTrail[^1];
        return new DocumentStatusResponse(
            d.Id,
            d.SourceDocumentId,
            d.Status.ToString(),
            last.Timestamp,
            d.PreviewSizeBytes,
            d.Status == DocumentStatus.Failed ? last.Detail : null);
    }
}
