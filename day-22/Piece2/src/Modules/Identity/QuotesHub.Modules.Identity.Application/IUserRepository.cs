using QuotesHub.Modules.Identity.Domain;

namespace QuotesHub.Modules.Identity.Application;

public interface IUserRepository
{
    Task AddAsync(User user, CancellationToken cancellationToken);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken);
}
