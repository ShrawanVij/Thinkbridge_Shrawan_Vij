using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuotesApi.Data;
using QuotesApi.Models;

namespace QuotesApi.Extensions;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        // LOGIN
        app.MapPost("/api/auth/login", async (
            LoginRequest request,
            QuoteDbContext db,
            IConfiguration configuration) =>
        {
            var user = await db.Users
                .FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user is null ||
                !BCrypt.Net.BCrypt.Verify(
                    request.Password,
                    user.PasswordHash))
            {
                return Results.Unauthorized();
            }

            var accessToken = CreateAccessToken(
                user,
                configuration);

            var rawRefreshToken = Convert.ToBase64String(
                RandomNumberGenerator.GetBytes(64));

            var refreshToken = new RefreshToken
            {
                Token = BCrypt.Net.BCrypt.HashPassword(
                    rawRefreshToken),

                UserId = user.Id,

                ExpiresAt = DateTime.UtcNow.AddDays(7)
            };

            db.RefreshTokens.Add(refreshToken);
            await db.SaveChangesAsync();

            var expiresInMinutes =
                configuration.GetValue<int>(
                    "Jwt:AccessTokenMinutes",
                    15);

            return Results.Ok(new
            {
                access_token = accessToken,
                refresh_token = rawRefreshToken,
                expires_in = expiresInMinutes * 60
            });
        });

        // REFRESH
        app.MapPost("/api/auth/refresh", async (
            RefreshRequest request,
            QuoteDbContext db,
            IConfiguration configuration,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("Auth");

            var tokens = await db.RefreshTokens
                .ToListAsync();

            var storedToken = tokens.FirstOrDefault(
                t => !string.IsNullOrEmpty(t.Token) &&
                     BCrypt.Net.BCrypt.Verify(
                         request.RefreshToken,
                         t.Token));

            if (storedToken is null)
                return Results.Unauthorized();

            // REUSE DETECTION
            if (storedToken.RevokedAt is not null &&
                storedToken.ReplacedByToken is not null)
            {
                logger.LogWarning(
                    "Refresh token reuse detected for UserId {UserId}. " +
                    "Revoking token family.",
                    storedToken.UserId);

                var userTokens = await db.RefreshTokens
                    .Where(t => t.UserId == storedToken.UserId)
                    .ToListAsync();

                foreach (var token in userTokens)
                {
                    token.RevokedAt ??= DateTime.UtcNow;
                }

                await db.SaveChangesAsync();

                return Results.Unauthorized();
            }

            // EXPIRED TOKEN
            if (storedToken.ExpiresAt <= DateTime.UtcNow)
                return Results.Unauthorized();

            var user = await db.Users
                .FirstOrDefaultAsync(
                    u => u.Id == storedToken.UserId);

            if (user is null)
                return Results.Unauthorized();

            // NEW ACCESS TOKEN
            var accessToken = CreateAccessToken(
                user,
                configuration);

            // NEW REFRESH TOKEN
            var rawNewRefreshToken =
                Convert.ToBase64String(
                    RandomNumberGenerator.GetBytes(64));

            var newRefreshToken = new RefreshToken
            {
                Token = BCrypt.Net.BCrypt.HashPassword(
                    rawNewRefreshToken),

                UserId = user.Id,

                ExpiresAt = DateTime.UtcNow.AddDays(7)
            };

            // ROTATE OLD TOKEN
            storedToken.RevokedAt = DateTime.UtcNow;
            storedToken.ReplacedByToken =
                rawNewRefreshToken;

            db.RefreshTokens.Add(newRefreshToken);

            await db.SaveChangesAsync();

            var expiresInMinutes =
                configuration.GetValue<int>(
                    "Jwt:AccessTokenMinutes",
                    15);

            return Results.Ok(new
            {
                access_token = accessToken,
                refresh_token = rawNewRefreshToken,
                expires_in = expiresInMinutes * 60
            });
        });
    }

    private static string CreateAccessToken(
        User user,
        IConfiguration configuration)
    {
        var key = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException(
                "JWT key is not configured.");

        var issuer = configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException(
                "JWT issuer is not configured.");

        var audience = configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException(
                "JWT audience is not configured.");

        var expiresInMinutes =
            configuration.GetValue<int>(
                "Jwt:AccessTokenMinutes",
                15);

        var claims = new[]
        {
            new Claim(
                JwtRegisteredClaimNames.Sub,
                user.Id.ToString()),

            new Claim(
                JwtRegisteredClaimNames.Email,
                user.Email)
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(
                expiresInMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }

    public record LoginRequest(
        string Email,
        string Password);

    public record RefreshRequest(
        string RefreshToken);
}