using QuotesApi.Modules.Collections.Models;

namespace QuotesApi.Modules.Collections.Repositories;

public interface ICollectionRepository
{
    Task<Collection?> GetById(int id, CancellationToken cancellationToken);

    Task Add(Collection collection, CancellationToken cancellationToken);

    Task Update(Collection collection, CancellationToken cancellationToken);

    Task Delete(Collection collection, CancellationToken cancellationToken);
}
