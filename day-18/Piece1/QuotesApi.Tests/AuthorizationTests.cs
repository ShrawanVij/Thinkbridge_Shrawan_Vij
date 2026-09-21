using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.TestHost;
using QuotesApi.Data;
using QuotesApi.Models;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

public class AuthorizationTests
{
    [Fact]
    public async Task CreateQuote_WithoutWriteScope_Returns403()
    {
        var factory = CreateFactory<TestAuthHandler>();
        var client = factory.CreateClient();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test");

        var response = await client.PostAsJsonAsync(
            "/api/quotes",
            new
            {
                author = "Test Author",
                text = "Test quote"
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteQuoteOwnedByAnotherUser_Returns403()
    {
        var factory = CreateFactory<TestAuthHandler>();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<QuoteDbContext>();

            db.Quotes.Add(new Quote
            {
                Author = "User 2",
                Text = "This belongs to User 2",
                UserId = 2
            });

            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test");

        var response = await client.DeleteAsync("/api/quotes/1");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ExpiredToken_Returns401()
    {
        var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var configuration =
            factory.Services.GetRequiredService<IConfiguration>();

        var key = configuration["Jwt:Key"]!;

        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "1")
            },
            expires: DateTime.UtcNow.AddMinutes(-5),
            signingCredentials:
                new Microsoft.IdentityModel.Tokens.SigningCredentials(
                    new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(key)),
                    Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256));

        var tokenString =
            new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
                .WriteToken(token);

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                tokenString);

        var response = await client.PostAsJsonAsync(
            "/api/quotes",
            new
            {
                author = "Expired User",
                text = "This should not be created"
            });

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsAccessTokenAndSetsRefreshTokenCookie()
    {
        var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email = "test@example.com",
                password = "Test123!"
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(body.TryGetProperty("access_token", out _));
        // The refresh token is never exposed to JS -- it's set as an
        // HttpOnly cookie, not returned in the JSON body.
        Assert.False(body.TryGetProperty("refresh_token", out _));

        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders));
        var refreshCookie = Assert.Single(setCookieHeaders, h => h.StartsWith("refreshToken="));
        Assert.Contains("httponly", refreshCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/auth", refreshCookie, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email = "test@example.com",
                password = "WrongPassword!"
            });

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }


    [Fact]
    public async Task Login_UnknownUser_Returns401()
    {
        var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email = "doesnotexist@example.com",
                password = "Test123!"
            });

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }


    [Fact]
    public async Task Refresh_InvalidToken_Returns401()
    {
        var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "Cookie",
            "refreshToken=completely-invalid-refresh-token");

        var response = await client.PostAsync(
            "/api/auth/refresh",
            new StringContent(string.Empty));

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task Refresh_NoCookiePresent_Returns401()
    {
        var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var response = await client.PostAsync(
            "/api/auth/refresh",
            new StringContent(string.Empty));

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }


    [Fact]
    public async Task Refresh_ExpiredToken_Returns401()
    {
        var factory = new WebApplicationFactory<Program>();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<QuoteDbContext>();

            db.RefreshTokens.Add(new RefreshToken
            {
                Token = BCrypt.Net.BCrypt.HashPassword(
                    "expired-refresh-token"),
                UserId = 1,
                ExpiresAt = DateTime.UtcNow.AddMinutes(-5)
            });

            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", "refreshToken=expired-refresh-token");

        var response = await client.PostAsync(
            "/api/auth/refresh",
            new StringContent(string.Empty));

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task Refresh_RevokedToken_Returns401()
    {
        var factory = new WebApplicationFactory<Program>();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<QuoteDbContext>();

            db.RefreshTokens.Add(new RefreshToken
            {
                Token = BCrypt.Net.BCrypt.HashPassword("revoked-token"),
                UserId = 1,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                RevokedAt = DateTime.UtcNow,
                ReplacedByToken = "replacement-token"
            });

            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", "refreshToken=revoked-token");

        var response = await client.PostAsync(
            "/api/auth/refresh",
            new StringContent(string.Empty));

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task Refresh_ValidCookie_RotatesTokenAndReturnsNewAccessToken()
    {
        var factory = new WebApplicationFactory<Program>();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<QuoteDbContext>();

            db.RefreshTokens.Add(new RefreshToken
            {
                Token = BCrypt.Net.BCrypt.HashPassword("valid-refresh-token"),
                UserId = 1,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            });

            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", "refreshToken=valid-refresh-token");

        var response = await client.PostAsync(
            "/api/auth/refresh",
            new StringContent(string.Empty));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("access_token", out _));
        Assert.False(body.TryGetProperty("refresh_token", out _));

        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders));
        var rotatedCookie = Assert.Single(setCookieHeaders, h => h.StartsWith("refreshToken="));
        Assert.DoesNotContain("refreshToken=valid-refresh-token;", rotatedCookie);
    }

    [Fact]
    public async Task CreateQuote_Anonymous_Returns401()
    {
        var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/quotes",
            new
            {
                author = "Test Author",
                text = "Test quote"
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateQuote_WithWriteScope_Returns201()
    {
        var factory = CreateFactory<WriteScopeTestAuthHandler>();
        var client = factory.CreateClient();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test");

        var response = await client.PostAsJsonAsync(
            "/api/quotes",
            new
            {
                author = "Test Author",
                text = "Authorized quote"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static WebApplicationFactory<Program>
        CreateFactory<THandler>()
        where THandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddAuthentication("Test")
                        .AddScheme<
                            AuthenticationSchemeOptions,
                            THandler>(
                            "Test",
                            _ => { });
                });
            });
    }
}

public class TestAuthHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        Microsoft.Extensions.Logging.ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult>
        HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Name, "test@example.com")
        };

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(
            AuthenticateResult.Success(ticket));
    }
}

public class WriteScopeTestAuthHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public WriteScopeTestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        Microsoft.Extensions.Logging.ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult>
        HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Name, "test@example.com"),
            new Claim("scope", "quotes.write")
        };

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(
            AuthenticateResult.Success(ticket));
    }
}