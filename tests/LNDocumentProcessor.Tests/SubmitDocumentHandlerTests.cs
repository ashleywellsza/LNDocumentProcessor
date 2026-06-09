using System.Text;
using LNDocumentProcessor.Api.Application.Abstractions;
using LNDocumentProcessor.Api.Application.Documents;
using LNDocumentProcessor.Api.Domain;
using LNDocumentProcessor.Api.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

namespace LNDocumentProcessor.Tests;

public sealed class SubmitDocumentHandlerTests
{
    [Fact]
    public async Task Resubmitting_same_external_document_returns_existing_record_without_storing_again()
    {
        var storage = new CountingStorageService();
        var repository = new InMemoryDocumentRepository();
        var handler = new SubmitDocumentHandler(
            repository, storage, TimeProvider.System, NullLogger<SubmitDocumentHandler>.Instance);

        var first = await handler.HandleAsync(NewCommand());
        var second = await handler.HandleAsync(NewCommand());

        // The second submission is recognised as a duplicate...
        Assert.False(first.IsDuplicate);
        Assert.True(second.IsDuplicate);

        // ...maps to the same internal record...
        Assert.Equal(first.Document.Id, second.Document.Id);

        // ...and does not write content a second time.
        Assert.Equal(1, storage.SaveCount);
    }

    [Fact]
    public async Task Fresh_submission_stores_content_and_records_received_then_stored()
    {
        var storage = new CountingStorageService();
        var repository = new InMemoryDocumentRepository();
        var handler = new SubmitDocumentHandler(
            repository, storage, TimeProvider.System, NullLogger<SubmitDocumentHandler>.Instance);

        var result = await handler.HandleAsync(NewCommand());

        Assert.Equal(DocumentStatus.Stored, result.Document.Status);
        Assert.Equal(
            new[] { DocumentStatus.Received, DocumentStatus.Stored },
            result.Document.AuditTrail.Select(a => a.Status));
        Assert.NotNull(result.Document.BlobReference);
        Assert.True(result.Document.ContentSizeBytes > 0);
    }

    private static SubmitDocumentCommand NewCommand() => new(
        SourceDocumentId: "SRC-123",
        Provider: "acme-legal",
        Title: "Sample Brief",
        Jurisdiction: "ZA",
        Categories: ["filing"],
        Tags: ["urgent"],
        ContentType: "text/plain",
        FileName: "brief.txt",
        Content: new MemoryStream(Encoding.UTF8.GetBytes("hello legal world")));

    /// <summary>Fake storage that records content and counts writes.</summary>
    private sealed class CountingStorageService : IStorageService
    {
        private readonly Dictionary<string, byte[]> _objects = new(StringComparer.Ordinal);

        public int SaveCount { get; private set; }

        public async Task<StorageResult> SaveAsync(
            string objectKey, Stream content, string contentType, CancellationToken cancellationToken = default)
        {
            SaveCount++;
            using var ms = new MemoryStream();
            await content.CopyToAsync(ms, cancellationToken);
            var bytes = ms.ToArray();
            _objects[objectKey] = bytes;
            return new StorageResult(objectKey, bytes.Length);
        }

        public Task<Stream?> OpenReadAsync(string reference, CancellationToken cancellationToken = default)
            => Task.FromResult<Stream?>(_objects.TryGetValue(reference, out var bytes) ? new MemoryStream(bytes) : null);
    }
}
