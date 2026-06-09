namespace LNDocumentProcessor.Api.Application.Abstractions;

/// <summary>
/// Produces a short preview/summary from document content. The default
/// implementation returns a text excerpt; a richer implementation (PDF text
/// extraction, OCR, LLM summary) can replace it behind this port.
/// </summary>
public interface IPreviewGenerator
{
    /// <summary>
    /// Generates preview text from the supplied content stream. The stream is
    /// read forward-only; the caller owns its lifetime.
    /// </summary>
    Task<string> GenerateAsync(Stream content, string contentType, CancellationToken cancellationToken = default);
}
