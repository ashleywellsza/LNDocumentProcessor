namespace LNDocumentProcessor.Api.Application.Abstractions;

/// <summary>
/// Object-storage port. The local implementation writes to the file system;
/// a cloud implementation (Azure Blob / AWS S3) implements the same contract,
/// so callers never depend on a specific provider.
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// Persists raw content and returns an opaque reference (key/path) that can
    /// later be passed to <see cref="OpenReadAsync"/>.
    /// </summary>
    Task<StorageResult> SaveAsync(
        string objectKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a readable stream over previously stored content, or returns null
    /// if no object exists for the reference.
    /// </summary>
    Task<Stream?> OpenReadAsync(string reference, CancellationToken cancellationToken = default);
}

/// <summary>Outcome of a storage write: the reference to read it back and the byte count written.</summary>
public sealed record StorageResult(string Reference, long SizeBytes);
