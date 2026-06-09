using LNDocumentProcessor.Api.Domain;

namespace LNDocumentProcessor.Api.Application.Documents;

/// <summary>
/// Outcome of a submit. <see cref="IsDuplicate"/> is true when an existing
/// record matched the dedup key, in which case no new storage write occurred
/// and <see cref="Document"/> is the pre-existing record.
/// </summary>
public sealed record SubmitDocumentResult(Document Document, bool IsDuplicate);
