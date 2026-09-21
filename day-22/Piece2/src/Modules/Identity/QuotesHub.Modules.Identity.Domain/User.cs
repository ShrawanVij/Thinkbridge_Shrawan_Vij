using QuotesHub.SharedKernel;

namespace QuotesHub.Modules.Identity.Domain;

public class User : AggregateRoot<UserId>
{
    public string Email { get; private set; } = "";
    public string PasswordHash { get; private set; } = "";

    private User() { }

    public static User Register(string email, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            throw new ArgumentException("A valid email is required.", nameof(email));
        }

        return new User { Id = UserId.New(), Email = email, PasswordHash = passwordHash };
    }
}

public readonly record struct UserId(Guid Value)
{
    public static UserId New() => new(Guid.NewGuid());
}
