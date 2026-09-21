using QuotesApi.Services;

namespace QuotesApi.Tests;

public class ClockTests
{
    [Fact]
    public void FakeClock_Returns_Fixed_Time()
    {
        var expected = new DateTimeOffset(
            2026, 8, 11, 10, 0, 0,
            TimeSpan.Zero);

        var clock = new FakeClock
        {
            UtcNow = expected
        };

        Assert.Equal(expected, clock.UtcNow);
    }
}