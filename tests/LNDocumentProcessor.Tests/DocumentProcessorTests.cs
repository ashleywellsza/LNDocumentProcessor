using System.Text;
using LNDocumentProcessor.Api.Application.Abstractions;
using LNDocumentProcessor.Api.Application.Processing;
using LNDocumentProcessor.Api.Domain;
using LNDocumentProcessor.Api.Infrastructure.Persistence;
using LNDocumentProcessor.Api.Infrastructure.Processing;
using Microsoft.Extensions.Logging.Abstractions;

namespace LNDocumentProcessor.Tests;

public sealed class DocumentProcessorTests
{
    [Fact]
    public async Task Processing_generates_preview_and_advances_to_processed()
    {
        var repository = new InMemoryDocumentRepository();
        var storage = new InMemoryStorage();
        var document = await SeedStoredDocumentAsync(
            repository, storage, "text/plain", "The quick brown fox jumps over the lazy dog.");

        var processor = NewProcessor(repository, storage);
        await processor.ProcessAsync(NewMessage(document));

        Assert.Equal(DocumentStatus.Processed, document.Status);
        Assert.NotNull(document.PreviewReference);
        Assert.True(document.PreviewSizeBytes > 0);
        Assert.Contains(DocumentStatus.Processing, document.AuditTrail.Select(a => a.Status));
        Assert.Contains(DocumentStatus.Processed, document.AuditTrail.Select(a => a.Status));

        // Preview content is retrievable and is an excerpt of the source text.
        await using var previewStream = await storage.OpenReadAsync(document.PreviewReference!);
        using var sr = new StreamReader(previewStream!);
        var preview = await sr.ReadToEndAsync();
        Assert.Contains("quick brown fox", preview);
    }

    [Fact]
    public async Task Processing_failure_is_recorded_as_failed_with_a_reason()
    {
        var repository = new InMemoryDocumentRepository();
        var storage = new InMemoryStorage { FailOnRead = true };
        var document = await SeedStoredDocumentAsync(repository, storage, "text/plain", "anything");

        var processor = NewProcessor(repository, storage);
        await processor.ProcessAsync(NewMessage(document));

        Assert.Equal(DocumentStatus.Failed, document.Status);
        var last = document.AuditTrail[^1];
        Assert.Equal(DocumentStatus.Failed, last.Status);
        Assert.False(string.IsNullOrWhiteSpace(last.Detail)); // failure reason captured for visibility
    }

    private static DocumentProcessor NewProcessor(IDocumentRepository repository, IStorageService storage)
        => new(
            repository,
            storage,
            new ExcerptPreviewGenerator(),
            new NullStatusNotifier(),
            TimeProvider.System,
            NullLogger<DocumentProcessor>.Instance);

    private static DocumentProcessingMessage NewMessage(Document document)
        => new(document.Id, document.SourceDocumentId, DocumentProcessingMessage.GeneratePreviewAction, DateTimeOffset.UtcNow);

    private static async Task<Document> SeedStoredDocumentAsync(
        IDocumentRepository repository, InMemoryStorage storage, string contentType, string content)
    {
        var now = DateTimeOffset.UtcNow;
        var metadata = new DocumentMetadata("Title", "ZA", ["cat"], ["tag"], contentType, "file.txt");
        var document = Document.Receive(Guid.NewGuid(), "acme-legal", "SRC-1", metadata, now);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var stored = await storage.SaveAsync($"acme-legal/{document.Id}/file.txt", stream, contentType);
        document.MarkStored(stored.Reference, stored.SizeBytes, now);
        document.MarkQueued(now);
        await repository.AddAsync(document);
        return document;
    }

    private sealed class InMemoryStorage : IStorageService
    {
        private readonly Dictionary<string, byte[]> _objects = new(StringComparer.Ordinal);

        public bool FailOnRead { get; init; }

        public async Task<StorageResult> SaveAsync(
            string objectKey, Stream content, string contentType, CancellationToken cancellationToken = default)
        {
            using var ms = new MemoryStream();
            await content.CopyToAsync(ms, cancellationToken);
            var bytes = ms.ToArray();
            _objects[objectKey] = bytes;
            return new StorageResult(objectKey, bytes.Length);
        }

        public Task<Stream?> OpenReadAsync(string reference, CancellationToken cancellationToken = default)
        {
            if (FailOnRead)
            {
                return Task.FromResult<Stream?>(null); // simulates content that cannot be read back
            }

            return Task.FromResult<Stream?>(_objects.TryGetValue(reference, out var bytes) ? new MemoryStream(bytes) : null);
        }
    }

    private sealed class NullStatusNotifier : IStatusNotifier
    {
        public Task NotifyAsync(
            Guid documentId, string sourceDocumentId, DocumentStatus status, long? previewSizeBytes,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
