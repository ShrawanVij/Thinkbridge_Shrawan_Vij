using System.Diagnostics;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Models;
using Xunit.Abstractions;

namespace QuotesApi.Tests;

public class ChangeTrackerBenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public ChangeTrackerBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static DbContextOptions<QuoteDbContext> BuildOptions()
    {
        var dbPath = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "QuotesApi", "quotes.db");

        return new DbContextOptionsBuilder<QuoteDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
    }

    [Fact]
    public void Tracked_Vs_NoTracking_10k_Rows()
    {
        var options = BuildOptions();

        using (var warmup = new QuoteDbContext(options))
        {
            warmup.Quotes.AsNoTracking().Take(1).ToList();
        }

        long allocBefore1 = GC.GetAllocatedBytesForCurrentThread();
        var swTracked = Stopwatch.StartNew();
        List<Quote> tracked;
        using (var ctx = new QuoteDbContext(options))
        {
            tracked = ctx.Quotes.ToList();
        }
        swTracked.Stop();
        long allocAfter1 = GC.GetAllocatedBytesForCurrentThread();

        long allocBefore2 = GC.GetAllocatedBytesForCurrentThread();
        var swNoTracking = Stopwatch.StartNew();
        List<Quote> noTracking;
        using (var ctx = new QuoteDbContext(options))
        {
            noTracking = ctx.Quotes.AsNoTracking().ToList();
        }
        swNoTracking.Stop();
        long allocAfter2 = GC.GetAllocatedBytesForCurrentThread();

        _output.WriteLine(
            $"Tracked:    {tracked.Count} rows, {swTracked.ElapsedMilliseconds} ms, " +
            $"{(allocAfter1 - allocBefore1).ToString("N0", CultureInfo.InvariantCulture)} bytes allocated");
        _output.WriteLine(
            $"NoTracking: {noTracking.Count} rows, {swNoTracking.ElapsedMilliseconds} ms, " +
            $"{(allocAfter2 - allocBefore2).ToString("N0", CultureInfo.InvariantCulture)} bytes allocated");
    }

    [Fact]
    public void Identity_Resolution_Tracked_Vs_NoTracking()
    {
        var options = BuildOptions();

        using var trackedCtx = new QuoteDbContext(options);
        var first = trackedCtx.Quotes.First(q => q.Id == 1);
        var second = trackedCtx.Quotes.First(q => q.Id == 1);

        _output.WriteLine(
            $"Tracked, same context, same PK queried twice - same instance: {ReferenceEquals(first, second)}");

        using var noTrackCtx = new QuoteDbContext(options);
        var third = noTrackCtx.Quotes.AsNoTracking().First(q => q.Id == 1);
        var fourth = noTrackCtx.Quotes.AsNoTracking().First(q => q.Id == 1);

        _output.WriteLine(
            $"AsNoTracking, same context, same PK queried twice - same instance: {ReferenceEquals(third, fourth)}");
    }
}
