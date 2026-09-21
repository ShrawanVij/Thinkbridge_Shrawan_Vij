using QuotesApi.Services;

public sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; }
}