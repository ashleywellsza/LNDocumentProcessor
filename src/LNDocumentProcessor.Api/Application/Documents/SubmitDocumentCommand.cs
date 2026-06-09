namespace LNDocumentProcessor.Api.Application.Documents;

/// <summary>
/// Input to the submit use case. <see cref="Content"/> is an open readable
/// stream over the raw document bytes; the caller owns its lifetime.
/// </summary>
public sealed record SubmitDocumentCommand(
    string SourceDocumentId,
    string Provider,
    string Title,
    string? Jurisdiction,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> Tags,
    string ContentType,
    string FileName,
    Stream Content);
