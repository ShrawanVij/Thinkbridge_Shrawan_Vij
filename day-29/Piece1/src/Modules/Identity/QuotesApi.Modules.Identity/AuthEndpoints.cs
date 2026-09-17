using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using QuotesApi.Modules.Identity.Data;
using QuotesApi.Modules.Identity.Models;

namespace QuotesApi.Modules.Identity;

public static class AuthEndpoints
{
    private const string RefreshTokenCookieName = "refreshToken";

    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/api/auth/login", async (
            LoginRequest request,
            HttpContext context,
            IdentityDbContext db,
            IOptions<JwtOptions> jwtOptions) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return Results.Unauthorized();
            }

            var accessToken = CreateAccessToken(user, jwtOptions.Value);
            var rawRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

            db.RefreshTokens.Add(new RefreshToken
            {
                Token = BCrypt.Net.BCrypt.HashPassword(rawRefreshToken),
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            });
            await db.SaveChangesAsync();

            SetRefreshTokenCookie(context, rawRefreshToken);

            var expiresInMinutes = (int)jwtOptions.Value.AccessTokenLifetime.TotalMinutes;

            return Results.Ok(new { access_token = accessToken, expires_in = expiresInMinutes * 60 });
        })
        .RequireRateLimiting("auth");

        app.MapPost("/api/auth/refresh", async (
            HttpContext context,
            IdentityDbContext db,
            IOptions<JwtOptions> jwtOptions,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("Auth");

            var rawRefreshTokenFromCookie = context.Request.Cookies[RefreshTokenCookieName];
            if (string.IsNullOrEmpty(rawRefreshTokenFromCookie))
            {
                return Results.Unauthorized();
            }

            var tokens = await db.RefreshTokens.ToListAsync();
            var storedToken = tokens.FirstOrDefault(
                t => !string.IsNullOrEmpty(t.Token) &&
                     BCrypt.Net.BCrypt.Verify(rawRefreshTokenFromCookie, t.Token));

            if (storedToken is null)
            {
                return Results.Unauthorized();
            }

            if (storedToken.RevokedAt is not null && storedToken.ReplacedByToken is not null)
            {
                logger.LogWarning(
                    "Refresh token reuse detected for UserId {UserId}. Revoking token family.",
                    storedToken.UserId);

                var userTokens = await db.RefreshTokens.Where(t => t.UserId == storedToken.UserId).ToListAsync();
                foreach (var token in userTokens)
                {
                    token.RevokedAt ??= DateTime.UtcNow;
                }
                await db.SaveChangesAsync();

                return Results.Unauthorized();
            }

            if (storedToken.RevokedAt is not null || storedToken.ExpiresAt <= DateTime.UtcNow)
            {
                return Results.Unauthorized();
            }

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == storedToken.UserId);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            var accessToken = CreateAccessToken(user, jwtOptions.Value);
            var rawNewRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

            storedToken.RevokedAt = DateTime.UtcNow;
            storedToken.ReplacedByToken = rawNewRefreshToken;

            db.RefreshTokens.Add(new RefreshToken
            {
                Token = BCrypt.Net.BCrypt.HashPassword(rawNewRefreshToken),
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            });

            await db.SaveChangesAsync();

            SetRefreshTokenCookie(context, rawNewRefreshToken);

            var expiresInMinutes = (int)jwtOptions.Value.AccessTokenLifetime.TotalMinutes;

            return Results.Ok(new { access_token = accessToken, expires_in = expiresInMinutes * 60 });
        });

        // LOGOUT — revokes the refresh token behind the cookie (so it can't
        // be replayed even though it hasn't expired) and clears the cookie.
        // Doesn't require a valid access token: a caller with an already-
        // expired access token still needs to be able to log out.
        app.MapPost("/api/auth/logout", async (
            HttpContext context,
            IdentityDbContext db) =>
        {
            var rawRefreshTokenFromCookie = context.Request.Cookies[RefreshTokenCookieName];

            if (!string.IsNullOrEmpty(rawRefreshTokenFromCookie))
            {
                var tokens = await db.RefreshTokens.Where(t => t.RevokedAt == null).ToListAsync();
                var storedToken = tokens.FirstOrDefault(
                    t => !string.IsNullOrEmpty(t.Token) &&
                         BCrypt.Net.BCrypt.Verify(rawRefreshTokenFromCookie, t.Token));

                if (storedToken is not null)
                {
                    storedToken.RevokedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                }
            }

            ClearRefreshTokenCookie(context);

            return Results.NoContent();
        });
    }

    private static void SetRefreshTokenCookie(HttpContext context, string rawRefreshToken)
    {
        context.Response.Cookies.Append(
            RefreshTokenCookieName,
            rawRefreshToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Path = "/api/auth",
                Expires = DateTimeOffset.UtcNow.AddDays(7),
            });
    }

    private static void ClearRefreshTokenCookie(HttpContext context)
    {
        context.Response.Cookies.Delete(
            RefreshTokenCookieName,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Path = "/api/auth",
            });
    }

    private static string CreateAccessToken(User user, JwtOptions options)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("scope", "quotes.write")
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(options.AccessTokenLifetime),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public record LoginRequest(string Email, string Password);
}
