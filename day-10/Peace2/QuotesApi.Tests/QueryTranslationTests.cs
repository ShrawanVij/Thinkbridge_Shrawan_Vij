using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuotesApi.Data;
using Xunit.Abstractions;

namespace QuotesApi.Tests;

public record QuoteSummaryDto(int Id, string Author, string Text);

public class QueryTranslationTests
{
    private readonly ITestOutputHelper _output;

    public QueryTranslationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private DbContextOptions<QuoteDbContext> BuildLoggedOptions(List<string> log)
    {
        var dbPath = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "QuotesApi", "quotes.db");

        return new DbContextOptionsBuilder<QuoteDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .LogTo(line => log.Add(line), LogLevel.Information)
            .EnableSensitiveDataLogging()
            .Options;
    }

    [Fact]
    public void WholeEntity_Query_Logs_All_Columns()
    {
        var log = new List<string>();
        using var ctx = new QuoteDbContext(BuildLoggedOptions(log));

        var quotes = ctx.Quotes.Where(q => q.Author == "Mark Twain").ToList();

        var sql = log.First(l => l.Contains("SELECT"));
        _output.WriteLine($"Rows returned: {quotes.Count}");
        _output.WriteLine("Generated SQL:");
        _output.WriteLine(sql);
    }

    [Fact]
    public void Projected_Query_Logs_Leaner_SQL()
    {
        var log = new List<string>();
        using var ctx = new QuoteDbContext(BuildLoggedOptions(log));

        var quotes = ctx.Quotes
            .Where(q => q.Author == "Mark Twain")
            .Select(q => new QuoteSummaryDto(q.Id, q.Author, q.Text))
            .ToList();

        var sql = log.First(l => l.Contains("SELECT"));
        _output.WriteLine($"Rows returned: {quotes.Count}");
        _output.WriteLine("Generated SQL:");
        _output.WriteLine(sql);
    }

    [Fact]
    public void Accidental_ClientSide_Evaluation_Caught_And_Fixed()
    {
        var logBad = new List<string>();
        using (var ctx = new QuoteDbContext(BuildLoggedOptions(logBad)))
        {
            // BUG: .ToList() runs first, pulling the whole table into memory,
            // then .Where() filters in plain C# (LINQ to Objects), not SQL.
            var quotes = ctx.Quotes.ToList().Where(q => q.Author == "Mark Twain").ToList();

            var sql = logBad.First(l => l.Contains("SELECT"));
            _output.WriteLine("BEFORE FIX - client-side evaluation:");
            _output.WriteLine($"Rows returned after filter: {quotes.Count}");
            _output.WriteLine("Generated SQL (note: no WHERE clause at all):");
            _output.WriteLine(sql);
        }

        var logGood = new List<string>();
        using (var ctx = new QuoteDbContext(BuildLoggedOptions(logGood)))
        {
            // FIX: .Where() runs before .ToList(), so it's still IQueryable
            // and gets translated into the SQL WHERE clause.
            var quotes = ctx.Quotes.Where(q => q.Author == "Mark Twain").ToList();

            var sql = logGood.First(l => l.Contains("SELECT"));
            _output.WriteLine("AFTER FIX - filter pushed to the database:");
            _output.WriteLine($"Rows returned: {quotes.Count}");
            _output.WriteLine("Generated SQL:");
            _output.WriteLine(sql);
        }
    }
}
