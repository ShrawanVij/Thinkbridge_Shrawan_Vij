using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Features.Quotes;
using QuotesApi.Models;
using Xunit.Abstractions;

namespace QuotesApi.Tests;

public class FeedQueryBenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public FeedQueryBenchmarkTests(ITestOutputHelper output)
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
    public async Task EfSelect_Vs_Dapper_Feed_Query_Timing()
    {
        var options = BuildOptions();

        using (var seedCtx = new QuoteDbContext(options))
        {
            if (!seedCtx.Tags.Any())
            {
                var classic = new Tag { Name = "classic" };
                var wisdom = new Tag { Name = "wisdom" };

                var firstFew = seedCtx.Quotes.OrderBy(q => q.Id).Take(5).ToList();
                foreach (var quote in firstFew)
                {
                    quote.Tags.Add(classic);
                    quote.Tags.Add(wisdom);
                }

                seedCtx.SaveChanges();
            }
        }

        const int page = 1;
        const int size = 20;
        const int iterations = 50;

        using (var warm = new QuoteDbContext(options))
        {
            await new GetQuoteFeedQueryHandler(warm)
                .Handle(new GetQuoteFeedQuery(page, size), CancellationToken.None);
        }

        using (var warm = new QuoteDbContext(options))
        {
            await new GetQuoteFeedDapperQueryHandler(warm)
                .Handle(new GetQuoteFeedDapperQuery(page, size), CancellationToken.None);
        }

        var efTimes = new List<double>();
        for (var i = 0; i < iterations; i++)
        {
            using var ctx = new QuoteDbContext(options);
            var sw = Stopwatch.StartNew();
            await new GetQuoteFeedQueryHandler(ctx)
                .Handle(new GetQuoteFeedQuery(page, size), CancellationToken.None);
            sw.Stop();
            efTimes.Add(sw.Elapsed.TotalMilliseconds);
        }

        var dapperTimes = new List<double>();
        for (var i = 0; i < iterations; i++)
        {
            using var ctx = new QuoteDbContext(options);
            var sw = Stopwatch.StartNew();
            await new GetQuoteFeedDapperQueryHandler(ctx)
                .Handle(new GetQuoteFeedDapperQuery(page, size), CancellationToken.None);
            sw.Stop();
            dapperTimes.Add(sw.Elapsed.TotalMilliseconds);
        }

        efTimes.Sort();
        dapperTimes.Sort();

        _output.WriteLine(
            $"EF:     {iterations} runs, avg {efTimes.Average():F3} ms, " +
            $"median {efTimes[iterations / 2]:F3} ms, min {efTimes[0]:F3} ms, max {efTimes[^1]:F3} ms");
        _output.WriteLine(
            $"Dapper: {iterations} runs, avg {dapperTimes.Average():F3} ms, " +
            $"median {dapperTimes[iterations / 2]:F3} ms, min {dapperTimes[0]:F3} ms, max {dapperTimes[^1]:F3} ms");
    }
}
