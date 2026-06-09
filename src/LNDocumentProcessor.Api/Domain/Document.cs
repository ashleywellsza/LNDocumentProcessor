namespace LNDocumentProcessor.Api.Domain;

/// <summary>
/// The internal aggregate for an ingested document. Owns its metadata, the
/// reference to where raw content was stored, current status, and an append-only
/// audit trail. State transitions go through the methods below so that every
/// status change is recorded consistently.
/// </summary>
public sealed class Document
{
    private readonly List<AuditEntry> _auditTrail = new();

    private Document(
        Guid id,
        string provider,
        string sourceDocumentId,
        DocumentMetadata metadata,
        DateTimeOffset receivedAt)
    {
        Id = id;
        Provider = provider;
        SourceDocumentId = sourceDocumentId;
        Metadata = metadata;
        Status = DocumentStatus.Received;
        _auditTrail.Add(new AuditEntry(DocumentStatus.Received, receivedAt));
    }

    /// <summary>Internal identifier assigned at intake.</summary>
    public Guid Id { get; }

    /// <summary>Upstream provider identifier; part of the deduplication key.</summary>
    public string Provider { get; }

    /// <summary>Provider's own document id; part of the deduplication key.</summary>
    public string SourceDocumentId { get; }

    public DocumentMetadata Metadata { get; }

    /// <summary>Reference (key/path) to the raw content in storage; null until stored.</summary>
    public string? BlobReference { get; private set; }

    /// <summary>Size in bytes of the stored raw content; null until stored.</summary>
    public long? ContentSizeBytes { get; private set; }

    /// <summary>Reference to the generated preview in storage; null until processed.</summary>
    public string? PreviewReference { get; private set; }

    /// <summary>Size in bytes of the generated preview; null until processed.</summary>
    public long? PreviewSizeBytes { get; private set; }

    public DocumentStatus Status { get; private set; }

    public IReadOnlyList<AuditEntry> AuditTrail => _auditTrail;

    /// <summary>
    /// The deduplication key for an external document: provider + sourceDocumentId.
    /// </summary>
    public static string DedupKey(string provider, string sourceDocumentId)
        => $"{provider}::{sourceDocumentId}";

    public string DedupKeyValue => DedupKey(Provider, SourceDocumentId);

    /// <summary>
    /// Creates a freshly received document. Status starts at Received and the
    /// audit trail records that transition.
    /// </summary>
    public static Document Receive(
        Guid id,
        string provider,
        string sourceDocumentId,
        DocumentMetadata metadata,
        DateTimeOffset receivedAt)
        => new(id, provider, sourceDocumentId, metadata, receivedAt);

    /// <summary>
    /// Records that raw content has been persisted to storage and advances
    /// status to Stored.
    /// </summary>
    public void MarkStored(string blobReference, long contentSizeBytes, DateTimeOffset timestamp)
    {
        BlobReference = blobReference;
        ContentSizeBytes = contentSizeBytes;
        Transition(DocumentStatus.Stored, timestamp);
    }

    /// <summary>Records that the document has been queued for background processing.</summary>
    public void MarkQueued(DateTimeOffset timestamp) => Transition(DocumentStatus.Queued, timestamp);

    /// <summary>Records that the worker has begun processing the document.</summary>
    public void MarkProcessing(DateTimeOffset timestamp) => Transition(DocumentStatus.Processing, timestamp);

    /// <summary>
    /// Records that a preview has been generated and stored, advancing status
    /// to Processed.
    /// </summary>
    public void MarkProcessed(string previewReference, long previewSizeBytes, DateTimeOffset timestamp)
    {
        PreviewReference = previewReference;
        PreviewSizeBytes = previewSizeBytes;
        Transition(DocumentStatus.Processed, timestamp);
    }

    /// <summary>Records a processing failure with a human-readable reason for visibility.</summary>
    public void MarkFailed(string reason, DateTimeOffset timestamp)
        => Transition(DocumentStatus.Failed, timestamp, reason);

    /// <summary>Records a status transition and appends it to the audit trail.</summary>
    public void Transition(DocumentStatus status, DateTimeOffset timestamp, string? detail = null)
    {
        Status = status;
        _auditTrail.Add(new AuditEntry(status, timestamp, detail));
    }
}

/// <summary>
/// Descriptive metadata supplied by the provider at submission time. Separated
/// from the identity/lifecycle concerns on <see cref="Document"/>.
/// </summary>
public sealed record DocumentMetadata(
    string Title,
    string? Jurisdiction,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> Tags,
    string ContentType,
    string FileName);
