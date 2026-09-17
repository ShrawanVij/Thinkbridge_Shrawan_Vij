using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using QuotesApi.Modules.Quotes.Features;
using QuotesApi.Modules.Quotes.Repositories;

namespace QuotesApi.Modules.Quotes;

public static class QuotesEndpoints
{
    private const int MaxFeedPageSize = 100;

    public static IEndpointRouteBuilder MapQuotesEndpoints(this IEndpointRouteBuilder app)
    {
        // Legacy surface (pre-CQRS) — list/get/delete by repository, kept as-is.
        app.MapGet("/api/quotes", async (
            IQuoteRepository repository,
            CancellationToken cancellationToken,
            int? page,
            int? size) =>
        {
            var currentPage = page ?? 1;
            var currentSize = size ?? 10;

            if (currentPage < 1 || currentSize < 1 || currentSize > MaxFeedPageSize)
            {
                return Results.BadRequest($"Page must be >= 1 and size must be between 1 and {MaxFeedPageSize}.");
            }

            var quotes = await repository.GetQuotesAsync(currentPage, currentSize, cancellationToken);
            return Results.Ok(quotes);
        });

        app.MapGet("/api/quotes/{id:int}", async (
            int id,
            IQuoteRepository repository,
            CancellationToken cancellationToken) =>
        {
            var quote = await repository.GetByIdAsync(id, cancellationToken);
            return quote is null ? Results.NotFound() : Results.Ok(quote);
        });

        app.MapDelete("/api/quotes/{id:int}", async (
            int id,
            IQuoteRepository repository,
            IAuthorizationService authorizationService,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var authorizationResult = await authorizationService.AuthorizeAsync(user, id, "can-delete-own-quote");
            if (!authorizationResult.Succeeded)
            {
                return Results.Forbid();
            }

            var deleted = await repository.DeleteAsync(id, cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        })
        .RequireAuthorization();

        // CQRS surface — create/edit/feed, via MediatR + the outbox pattern.
        var cqrsQuotes = app.MapGroup("/cqrs/quotes");

        cqrsQuotes.MapPost("", async (
            CreateQuoteRequest request,
            ClaimsPrincipal user,
            IMediator mediator) =>
        {
            if (!TryGetUserId(user, out var userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var result = await mediator.Send(new CreateQuoteCommand(request.Author, request.Text, userId));
                return Results.Created($"/cqrs/quotes/{result.Id}", result);
            }
            catch (QuoteValidationException ex)
            {
                return Results.ValidationProblem(ex.Errors);
            }
        })
        .RequireAuthorization("can-edit-quotes");

        cqrsQuotes.MapPut("/{id:int}", async (
            int id,
            UpdateQuoteRequest request,
            IAuthorizationService authorizationService,
            ClaimsPrincipal user,
            IMediator mediator) =>
        {
            var authorizationResult = await authorizationService.AuthorizeAsync(user, id, "can-edit-own-quote");
            if (!authorizationResult.Succeeded)
            {
                return Results.Forbid();
            }

            try
            {
                var result = await mediator.Send(new UpdateQuoteCommand(id, request.Author, request.Text));
                return result is null ? Results.NotFound() : Results.Ok(result);
            }
            catch (QuoteValidationException ex)
            {
                return Results.ValidationProblem(ex.Errors);
            }
        })
        .RequireAuthorization();

        cqrsQuotes.MapGet("/feed", async (
            IMediator mediator,
            int? page,
            int? size,
            string? sort) =>
        {
            var currentPage = page ?? 1;
            var currentSize = size ?? 20;

            if (currentPage < 1 || currentSize < 1 || currentSize > MaxFeedPageSize)
            {
                return Results.BadRequest($"Page must be >= 1 and size must be between 1 and {MaxFeedPageSize}.");
            }

            var sortOrder = sort?.ToLowerInvariant() switch
            {
                "oldest" => QuoteSortOrder.OldestFirst,
                "author" => QuoteSortOrder.AuthorAsc,
                _ => QuoteSortOrder.NewestFirst,
            };

            var result = await mediator.Send(new GetQuoteFeedQuery(currentPage, currentSize, sortOrder));
            return Results.Ok(result);
        });

        cqrsQuotes.MapGet("/feed-dapper", async (
            IMediator mediator,
            int? page,
            int? size) =>
        {
            var currentPage = page ?? 1;
            var currentSize = size ?? 20;

            if (currentPage < 1 || currentSize < 1 || currentSize > MaxFeedPageSize)
            {
                return Results.BadRequest($"Page must be >= 1 and size must be between 1 and {MaxFeedPageSize}.");
            }

            var result = await mediator.Send(new GetQuoteFeedDapperQuery(currentPage, currentSize));
            return Results.Ok(result);
        });

        return app;
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out int userId)
    {
        var userIdClaim = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return int.TryParse(userIdClaim, out userId);
    }
}
