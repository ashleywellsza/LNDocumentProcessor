namespace LNDocumentProcessor.Api.Domain;

/// <summary>
/// A single, immutable entry in a document's audit trail: the status the
/// document entered and when. Optional <see cref="Detail"/> captures context
/// such as an error message on a Failed transition.
/// </summary>
public sealed record AuditEntry(DocumentStatus Status, DateTimeOffset Timestamp, string? Detail = null);
