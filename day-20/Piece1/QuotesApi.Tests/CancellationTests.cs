using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuotesApi.Models;
using QuotesApi.Repositories;

namespace QuotesApi.Tests;

public class CancellationTests
{
    [Fact]
    public async Task RequestCancellation_DoesNotComplete()
    {
        var repositoryStarted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        await using var factory =
            new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureServices(services =>
                    {
                        services.RemoveAll<ICollectionRepository>();

                        services.AddScoped<ICollectionRepository>(_ =>
                            new SlowCollectionRepository(
                                repositoryStarted));

                        services.AddAuthentication("Test")
                            .AddScheme<
                                Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
                                CancellationTestAuthHandler>(
                                "Test",
                                _ => { });
                    });
                });

        using var client = factory.CreateClient();

        using var cts = new CancellationTokenSource();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Test");

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/collections/1/items")
        {
            Content = new StringContent(
                """{"quoteId":1}""",
                System.Text.Encoding.UTF8,
                "application/json")
        };

        var responseTask = client.SendAsync(
            request,
            cts.Token);

        // Wait until the repository is actually reached.
        await repositoryStarted.Task;

        // Now cancellation is guaranteed to happen
        // while the repository is waiting.
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await responseTask);
    }
}


public class SlowCollectionRepository : ICollectionRepository
{
    private readonly TaskCompletionSource<bool> _started;

    public SlowCollectionRepository(
        TaskCompletionSource<bool> started)
    {
        _started = started;
    }

    public async Task<Collection?> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        _started.TrySetResult(true);

        await Task.Delay(
            TimeSpan.FromSeconds(10),
            cancellationToken);

        return new Collection(
            1,
            "Test Collection");
    }

    public Task Add(
        Collection collection,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task Update(
        Collection collection,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task Delete(
        Collection collection,
        CancellationToken cancellationToken)
        => Task.CompletedTask;
}


public class CancellationTestAuthHandler
    : Microsoft.AspNetCore.Authentication.AuthenticationHandler<
        Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions>
{
    public CancellationTestAuthHandler(
        Microsoft.Extensions.Options.IOptionsMonitor<
            Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions>
            options,
        Microsoft.Extensions.Logging.ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<
        Microsoft.AspNetCore.Authentication.AuthenticateResult>
        HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new System.Security.Claims.Claim(
                System.Security.Claims.ClaimTypes.NameIdentifier,
                "1"),

            new System.Security.Claims.Claim(
                "scope",
                "quotes.write")
        };

        var identity =
            new System.Security.Claims.ClaimsIdentity(
                claims,
                "Test");

        var principal =
            new System.Security.Claims.ClaimsPrincipal(
                identity);

        var ticket =
            new Microsoft.AspNetCore.Authentication.AuthenticationTicket(
                principal,
                "Test");

        return Task.FromResult(
            Microsoft.AspNetCore.Authentication.AuthenticateResult.Success(
                ticket));
    }
}