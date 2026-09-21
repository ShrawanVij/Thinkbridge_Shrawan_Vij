using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace QuotesApi.Tests;

public class RegisterTests
{
    [Fact]
    public async Task Register_NewEmail_CreatesAccountAndIssuesTokens()
    {
        var factory = TestFactory.CreateFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = "new-user@example.com", password = "StrongPass123" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("access_token", out _));

        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders));
        Assert.Contains(setCookieHeaders, h => h.StartsWith("refreshToken="));
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var factory = TestFactory.CreateFactory();
        var client = factory.CreateClient();

        var first = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = "duplicate@example.com", password = "StrongPass123" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = "duplicate@example.com", password = "AnotherPass456" });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Register_ShortPassword_Returns400()
    {
        var factory = TestFactory.CreateFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = "short-pw@example.com", password = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_ThenLogin_Succeeds()
    {
        var factory = TestFactory.CreateFactory();
        var client = factory.CreateClient();

        await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = "login-after-register@example.com", password = "StrongPass123" });

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = "login-after-register@example.com", password = "StrongPass123" });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }
}
