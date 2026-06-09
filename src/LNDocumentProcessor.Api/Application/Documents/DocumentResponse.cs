using LNDocumentProcessor.Api.Domain;

namespace LNDocumentProcessor.Api.Application.Documents;

/// <summary>API-facing projection of a <see cref="Document"/>, including its audit trail.</summary>
public sealed record DocumentResponse(
    Guid DocumentId,
    string SourceDocumentId,
    string Provider,
    string Title,
    string? Jurisdiction,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> Tags,
    string ContentType,
    string FileName,
    string Status,
    long? ContentSizeBytes,
    long? PreviewSizeBytes,
    IReadOnlyList<AuditEntryResponse> AuditTrail)
{
    public static DocumentResponse FromDomain(Document d) => new(
        d.Id,
        d.SourceDocumentId,
        d.Provider,
        d.Metadata.Title,
        d.Metadata.Jurisdiction,
        d.Metadata.Categories,
        d.Metadata.Tags,
        d.Metadata.ContentType,
        d.Metadata.FileName,
        d.Status.ToString(),
        d.ContentSizeBytes,
        d.PreviewSizeBytes,
        d.AuditTrail.Select(a => new AuditEntryResponse(a.Status.ToString(), a.Timestamp, a.Detail)).ToList());
}

public sealed record AuditEntryResponse(string Status, DateTimeOffset Timestamp, string? Detail);
