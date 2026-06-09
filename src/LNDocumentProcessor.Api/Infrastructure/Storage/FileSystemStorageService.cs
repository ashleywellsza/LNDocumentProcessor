using LNDocumentProcessor.Api.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace LNDocumentProcessor.Api.Infrastructure.Storage;

/// <summary>
/// Local object-storage substitute that writes content to the file system.
/// The object key is used as a relative path under the configured base path,
/// mirroring how a blob key / S3 key maps to an object. Swapping to Azure Blob
/// or S3 means replacing this class only.
/// </summary>
public sealed class FileSystemStorageService : IStorageService
{
    private readonly string _basePath;
    private readonly ILogger<FileSystemStorageService> _logger;

    public FileSystemStorageService(
        IOptions<FileSystemStorageOptions> options,
        ILogger<FileSystemStorageService> logger)
    {
        _basePath = Path.GetFullPath(options.Value.BasePath);
        _logger = logger;
        Directory.CreateDirectory(_basePath);
    }

    public async Task<StorageResult> SaveAsync(
        string objectKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var fullPath = ResolvePath(objectKey);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using (var file = new FileStream(
            fullPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true))
        {
            await content.CopyToAsync(file, cancellationToken);
        }

        var size = new FileInfo(fullPath).Length;
        _logger.LogDebug("Wrote {Bytes} bytes to {Path}.", size, fullPath);

        // The object key is the portable reference; consumers never see the absolute path.
        return new StorageResult(objectKey, size);
    }

    public Task<Stream?> OpenReadAsync(string reference, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolvePath(reference);
        if (!File.Exists(fullPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(
            fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
        return Task.FromResult<Stream?>(stream);
    }

    /// <summary>
    /// Resolves an object key to an absolute path and guards against path
    /// traversal escaping the base directory.
    /// </summary>
    private string ResolvePath(string objectKey)
    {
        var combined = Path.GetFullPath(Path.Combine(_basePath, objectKey));
        if (!combined.StartsWith(_basePath, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Object key '{objectKey}' resolves outside the storage root.");
        }

        return combined;
    }
}
