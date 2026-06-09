namespace LNDocumentProcessor.Api.Domain;

/// <summary>
/// Lifecycle states for a document. Task 1 covers Received -> Stored.
/// Later tasks extend this with Queued -> Processing -> Processed / Failed.
/// </summary>
public enum DocumentStatus
{
    Received,
    Stored,
    Queued,
    Processing,
    Processed,
    Failed
}
