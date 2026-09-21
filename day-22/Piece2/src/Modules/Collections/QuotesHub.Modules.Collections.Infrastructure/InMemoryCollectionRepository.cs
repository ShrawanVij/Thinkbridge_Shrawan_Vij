using System.Collections.Concurrent;
using QuotesHub.Modules.Collections.Application;
using QuotesHub.Modules.Collections.Domain;

namespace QuotesHub.Modules.Collections.Infrastructure;

// Placeholder persistence for the kickoff scaffold — proves the module
// boundary and DI wiring end-to-end without committing to a storage choice
// yet. Swapping this for an EF Core-backed repository (same pattern as
// Quotes.Infrastructure) doesn't touch Domain or Application at all.
public class InMemoryCollectionRepository : ICollectionRepository
{
    private readonly ConcurrentDictionary<CollectionId, Collection> _collections = new();

    public Task AddAsync(Collection collection, CancellationToken cancellationToken)
    {
        _collections[collection.Id] = collection;
        return Task.CompletedTask;
    }

    public Task<Collection?> GetByIdAsync(CollectionId id, CancellationToken cancellationToken) =>
        Task.FromResult(_collections.GetValueOrDefault(id));
}
