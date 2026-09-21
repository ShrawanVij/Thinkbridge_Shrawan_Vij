using QuotesHub.Modules.Collections.Domain;

namespace QuotesHub.Modules.Collections.Application;

public interface ICollectionRepository
{
    Task AddAsync(Collection collection, CancellationToken cancellationToken);
    Task<Collection?> GetByIdAsync(CollectionId id, CancellationToken cancellationToken);
}
