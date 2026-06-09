using LNDocumentProcessor.Api.Domain;

namespace LNDocumentProcessor.Api.Application.Abstractions;

/// <summary>
/// Persistence port for document metadata and audit trail. The local
/// implementation is in-memory; the contract is compatible with a real
/// database (EF Core / Cosmos) behind the same interface.
/// </summary>
public interface IDocumentRepository
{
    /// <summary>Looks up a document by its deduplication key (provider + sourceDocumentId).</summary>
    Task<Document?> GetByDedupKeyAsync(string provider, string sourceDocumentId, CancellationToken cancellationToken = default);

    /// <summary>Looks up a document by its internal id.</summary>
    Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Adds a new document. Implementations enforce dedup-key uniqueness.</summary>
    Task AddAsync(Document document, CancellationToken cancellationToken = default);

    /// <summary>Persists changes to an existing document (status/audit updates).</summary>
    Task UpdateAsync(Document document, CancellationToken cancellationToken = default);
}
