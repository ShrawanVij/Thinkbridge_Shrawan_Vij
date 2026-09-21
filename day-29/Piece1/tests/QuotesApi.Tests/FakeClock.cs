using QuotesApi.Contracts;

namespace QuotesApi.Tests;

public sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; }
}
