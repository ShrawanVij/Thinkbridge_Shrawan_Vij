using System.Collections.Concurrent;
using QuotesHub.Modules.Identity.Application;
using QuotesHub.Modules.Identity.Domain;

namespace QuotesHub.Modules.Identity.Infrastructure;

public class InMemoryUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<string, User> _usersByEmail = new();

    public Task AddAsync(User user, CancellationToken cancellationToken)
    {
        _usersByEmail[user.Email] = user;
        return Task.CompletedTask;
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
        Task.FromResult(_usersByEmail.GetValueOrDefault(email));
}
