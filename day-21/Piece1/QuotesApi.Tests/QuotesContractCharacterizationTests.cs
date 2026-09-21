using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

public class QuotesContractCharacterizationTests
{
    [Fact]
    public async Task GetQuotes_WithPageAndSize_ReturnsRealShape()
    {
        var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/quotes?page=1&size=3");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var quotes = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, quotes.ValueKind);
        Assert.True(quotes.GetArrayLength() <= 3);

        foreach (var quote in quotes.EnumerateArray())
        {
            Assert.True(quote.TryGetProperty("id", out _));
            Assert.True(quote.TryGetProperty("author", out _));
            Assert.True(quote.TryGetProperty("text", out _));
        }
    }

    [Fact]
    public async Task GetQuotes_InvalidPage_Returns400AsPlainString_NotProblemDetails()
    {
        // Pins a real gotcha: unlike the write endpoints, this 400 is a bare
        // string via Results.BadRequest(string), not { title, status, errors }.
        var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/quotes?page=0&size=5");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/json; charset=utf-8", response.Content.Headers.ContentType?.ToString());

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("\"errors\"", body);
        Assert.DoesNotContain("\"title\"", body);
        Assert.Contains("Page must be >= 1", body);
    }

    [Fact]
    public async Task CreateQuote_MissingFields_ReturnsRealValidationProblemDetails()
    {
        var factory = CreateAuthorizedFactory();
        var client = factory.CreateClient();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test");

        var response = await client.PostAsJsonAsync(
            "/api/quotes",
            new { author = "", text = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.TryGetProperty("errors", out var errors));
        Assert.True(errors.TryGetProperty("author", out var authorErrors));
        Assert.True(errors.TryGetProperty("text", out var textErrors));
        Assert.Contains("Author is required.", authorErrors.EnumerateArray().Select(e => e.GetString()));
        Assert.Contains("Text is required.", textErrors.EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task CreateQuote_Anonymous_Returns401NotProblemDetails()
    {
        var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/quotes",
            new { author = "Someone", text = "Something" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateAuthorizedFactory()
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddAuthentication("Test")
                        .AddScheme<AuthenticationSchemeOptions, WriteScopeTestAuthHandler>(
                            "Test",
                            _ => { });
                });
            });
    }
}