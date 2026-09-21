using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using QuotesApi.Modules.Collections.Data;
using QuotesApi.Modules.Engagement.Data;
using QuotesApi.Modules.Identity.Data;
using QuotesApi.Modules.Quotes.Data;

namespace QuotesApi.Tests;

// Shared by every test file: every factory gets its own on-disk SQLite files
// (a shared default path would let one test's inserted rows — and their
// auto-increment ids — leak into another's assertions, since nothing here
// deletes them between tests).
public static class TestFactory
{
    public static WebApplicationFactory<Program> CreateFactory<THandler>()
        where THandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        return CreateFactory(services =>
            services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, THandler>("Test", _ => { }));
    }

    public static WebApplicationFactory<Program> CreateFactory(Action<IServiceCollection>? configureServices = null)
    {
        var runId = Guid.NewGuid();

        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    IsolateSqliteDb<QuotesDbContext>(services, $"quotes-test-{runId}.db");
                    IsolateSqliteDb<IdentityDbContext>(services, $"identity-test-{runId}.db");
                    IsolateSqliteDb<CollectionsDbContext>(services, $"collections-test-{runId}.db");
                    IsolateSqliteDb<EngagementDbContext>(services, $"engagement-test-{runId}.db");

                    configureServices?.Invoke(services);
                });
            });
    }

    private static void IsolateSqliteDb<TContext>(IServiceCollection services, string fileName)
        where TContext : DbContext
    {
        services.RemoveAll<DbContextOptions<TContext>>();
        services.AddDbContext<TContext>(options =>
            options.UseSqlite($"Data Source={Path.Combine(Path.GetTempPath(), fileName)}"));
    }
}

// Reads the user id straight from the Authorization header's parameter
// (e.g. "Test 2") instead of always claiming to be the same fixed user —
// needed for tests that check one user can't reach another user's data,
// where both requests have to hit the same running app/database but
// authenticate as different callers.
public class MultiUserTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public MultiUserTestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        Microsoft.Extensions.Logging.ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var userId = Request.Headers.Authorization.ToString() is { Length: > 0 } header &&
                     System.Net.Http.Headers.AuthenticationHeaderValue.TryParse(header, out var parsed) &&
                     !string.IsNullOrEmpty(parsed.Parameter)
            ? parsed.Parameter
            : "1";

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, $"user{userId}@example.com"),
            new Claim("scope", "quotes.write")
        };

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
