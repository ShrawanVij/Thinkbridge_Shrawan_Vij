using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Models;
using QuotesApi.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using System.IdentityModel.Tokens.Jwt;

namespace QuotesApi.Extensions;

public static class QuoteApiExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services)
    {
        services.AddDbContext<QuoteDbContext>(options =>
        {
            var dbPath = OperatingSystem.IsWindows()
                ? Path.Combine(AppContext.BaseDirectory, "quotes.db")
                : "/tmp/quotes.db";

            options.UseSqlite($"Data Source={dbPath}");
        });

        services.AddScoped<IQuoteRepository, QuoteRepository>();

        return services;
    }

    public static IEndpointRouteBuilder MapQuoteEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/quotes", async (
            IQuoteRepository repository,
            CancellationToken cancellationToken,
            ILoggerFactory loggerFactory,
            int? page,
            int? size) =>
        {
            var logger = loggerFactory.CreateLogger("QuoteApi");
            var currentPage = page ?? 1;
            var currentSize = size ?? 10;

            logger.LogInformation(
                "Starting quote request for page {Page} with size {Size}",
                currentPage,
                currentSize);

            if (currentPage < 1 || currentSize < 1 || currentSize > 100)
            {
                logger.LogWarning(
                    "Invalid pagination requested: page={Page}, size={Size}",
                    currentPage,
                    currentSize);

                return Results.BadRequest(
                    "Page must be >= 1 and size must be between 1 and 100.");
            }

            logger.LogInformation(
                "Fetching quotes {Page} {Size}",
                currentPage,
                currentSize);

            logger.LogInformation(
                "Querying quote repository for page {Page}",
                currentPage,
                currentSize);

            var quotes = await repository.GetQuotesAsync(
                currentPage,
                currentSize,
                cancellationToken);

            logger.LogInformation(
                "Fetched {Count} quotes",
                quotes.Count);

            logger.LogInformation(
                "Quote request completed successfully with {Count} results",
                quotes.Count);

            return Results.Ok(quotes);
        });

        app.MapGet("/api/quotes/{id:int}", async (
            int id,
            IQuoteRepository repository,
            CancellationToken cancellationToken,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("QuoteApi");

            logger.LogInformation(
                "Starting quote request for {QuoteId}",
                id);

            var quote = await repository.GetByIdAsync(
                id,
                cancellationToken);

            if (quote is null)
            {
                logger.LogWarning(
                    "Quote {QuoteId} was not found",
                    id);

                return Results.NotFound();
            }

            logger.LogInformation(
                "Quote {QuoteId} retrieved successfully",
                id);

            return Results.Ok(quote);
        });

        app.MapPost("/api/quotes", async (
            Quote quote,
            ClaimsPrincipal user,
            IQuoteRepository repository,
            CancellationToken cancellationToken,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("QuoteApi");

            if (string.IsNullOrWhiteSpace(quote.Author) ||
                string.IsNullOrWhiteSpace(quote.Text))
            {
                logger.LogWarning(
                    "Invalid quote submitted: author or text is missing.");

                var errors = new Dictionary<string, string[]>();

                if (string.IsNullOrWhiteSpace(quote.Author))
                {
                    errors["author"] = new[]
                    {
                        "Author is required."
                    };
                }

                if (string.IsNullOrWhiteSpace(quote.Text))
                {
                    errors["text"] = new[]
                    {
                        "Text is required."
                    };
                }

                return Results.ValidationProblem(errors);
            }

            if (quote.Author.Length > 100)
            {
                logger.LogWarning(
                    "Invalid quote submitted: author exceeds 100 characters.");

                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["author"] = new[]
                        {
                            "Author cannot exceed 100 characters."
                        }
                    });
            }

            if (quote.Text.Length > 1000)
            {
                logger.LogWarning(
                    "Invalid quote submitted: text exceeds 1000 characters.");

                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["text"] = new[]
                        {
                            "Text cannot exceed 1000 characters."
                        }
                    });
            }

            quote.Id = 0;

            var userIdClaim = user.FindFirst(
                JwtRegisteredClaimNames.Sub)?.Value
                ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userIdClaim, out var userId))
            {
                return Results.Unauthorized();
            }

            quote.UserId = userId;

            var createdQuote = await repository.AddAsync(
                quote,
                cancellationToken);

            logger.LogInformation(
                "Created quote with ID {QuoteId}",
                createdQuote.Id);

            return Results.Created(
                $"/api/quotes/{createdQuote.Id}",
                createdQuote);
        })
        .RequireAuthorization("can-edit-quotes");

        app.MapDelete("/api/quotes/{id:int}", async (
            int id,
            IQuoteRepository repository,
            IAuthorizationService authorizationService,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var authorizationResult =
                await authorizationService.AuthorizeAsync(
                    user,
                    id,
                    "can-delete-own-quote");

            if (!authorizationResult.Succeeded)
            {
                return Results.Forbid();
            }

            var deleted = await repository.DeleteAsync(
                id,
                cancellationToken);

            return deleted
                ? Results.NoContent()
                : Results.NotFound();
        })
        .RequireAuthorization();

        return app;
    }
}