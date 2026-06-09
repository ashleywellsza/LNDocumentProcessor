using System.Collections.Concurrent;
using LNDocumentProcessor.Api.Application.Abstractions;
using LNDocumentProcessor.Api.Domain;

namespace LNDocumentProcessor.Api.Infrastructure.Persistence;

/// <summary>
/// In-memory metadata store with a secondary index on the deduplication key
/// (provider + sourceDocumentId). Registered as a singleton so state is shared
/// for the lifetime of the process. State is lost on restart — acceptable for
/// the assignment scope; the interface is compatible with a durable store.
/// </summary>
public sealed class InMemoryDocumentRepository : IDocumentRepository
{
    private readonly ConcurrentDictionary<Guid, Document> _byId = new();
    private readonly ConcurrentDictionary<string, Guid> _byDedupKey = new(StringComparer.Ordinal);

    public Task<Document?> GetByDedupKeyAsync(
        string provider, string sourceDocumentId, CancellationToken cancellationToken = default)
    {
        var key = Document.DedupKey(provider, sourceDocumentId);
        if (_byDedupKey.TryGetValue(key, out var id) && _byId.TryGetValue(id, out var document))
        {
            return Task.FromResult<Document?>(document);
        }

        return Task.FromResult<Document?>(null);
    }

    public Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_byId.TryGetValue(id, out var document) ? document : null);

    public Task AddAsync(Document document, CancellationToken cancellationToken = default)
    {
        // Reserve the dedup key first so concurrent submissions of the same
        // external document cannot both create a record.
        if (!_byDedupKey.TryAdd(document.DedupKeyValue, document.Id))
        {
            throw new InvalidOperationException(
                $"A document with dedup key '{document.DedupKeyValue}' already exists.");
        }

        _byId[document.Id] = document;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Document document, CancellationToken cancellationToken = default)
    {
        // The stored instance is the same reference that callers mutate, so
        // there is nothing to flush for the in-memory store. Kept for contract
        // parity with a durable implementation.
        _byId[document.Id] = document;
        return Task.CompletedTask;
    }
}
