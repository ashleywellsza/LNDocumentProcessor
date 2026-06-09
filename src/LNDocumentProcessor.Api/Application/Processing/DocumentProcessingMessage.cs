namespace LNDocumentProcessor.Api.Application.Processing;

/// <summary>
/// Message handed from the intake step to the background worker. Mirrors the
/// fields a real queue payload would carry (documentId, sourceDocumentId,
/// action, submittedAt) so the in-memory queue stays compatible with SQS /
/// Service Bus.
/// </summary>
public sealed record DocumentProcessingMessage(
    Guid DocumentId,
    string SourceDocumentId,
    string Action,
    DateTimeOffset SubmittedAt)
{
    public const string GeneratePreviewAction = "generate-preview";
}
