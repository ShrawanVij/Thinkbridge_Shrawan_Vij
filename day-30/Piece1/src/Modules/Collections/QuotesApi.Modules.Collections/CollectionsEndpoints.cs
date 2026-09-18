using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using QuotesApi.Contracts;
using QuotesApi.Modules.Collections.Models;
using QuotesApi.Modules.Collections.Repositories;

namespace QuotesApi.Modules.Collections;

public static class CollectionsEndpoints
{
    public static void MapCollectionsEndpoints(this WebApplication app)
    {
        // Owner comes from the authenticated caller, never the request body —
        // taking OwnerId from the client meant any caller could create a
        // collection "owned" by someone else's id.
        app.MapPost("/collections", async (
            CreateCollectionRequest request,
            ClaimsPrincipal user,
            ICollectionRepository repository,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(user, out var ownerId))
                return Results.Unauthorized();

            var collection = new Collection(ownerId, request.Name);
            await repository.Add(collection, cancellationToken);
            return Results.Ok(collection);
        })
        .RequireAuthorization("can-edit-collections");

        // The caller's own collections — never another user's.
        app.MapGet("/collections", async (
            ClaimsPrincipal user,
            ICollectionRepository repository,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(user, out var ownerId))
                return Results.Unauthorized();

            var collections = await repository.GetByOwnerId(ownerId, cancellationToken);
            return Results.Ok(collections);
        })
        .RequireAuthorization("can-edit-collections");

        app.MapGet("/collections/{id:int}", async (
            int id,
            ClaimsPrincipal user,
            ICollectionRepository repository,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(user, out var userId))
                return Results.Unauthorized();

            var collection = await repository.GetById(id, cancellationToken);
            if (collection is null)
                return Results.NotFound();

            if (collection.OwnerId != userId)
                return Results.Forbid();

            return Results.Ok(collection);
        })
        .RequireAuthorization("can-edit-collections");

        app.MapPost("/collections/{id:int}/items", async (
            int id,
            AddCollectionItemRequest request,
            ClaimsPrincipal user,
            ICollectionRepository repository,
            IClock clock,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(user, out var userId))
                return Results.Unauthorized();

            var collection = await repository.GetById(id, cancellationToken);
            if (collection is null)
                return Results.NotFound();

            if (collection.OwnerId != userId)
                return Results.Forbid();

            collection.AddItem(request.QuoteId, clock);
            await repository.Update(collection, cancellationToken);

            return Results.Ok(collection);
        })
        .RequireAuthorization("can-edit-collections");

        app.MapDelete("/collections/{id:int}/items/{quoteId:int}", async (
            int id,
            int quoteId,
            ClaimsPrincipal user,
            ICollectionRepository repository,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(user, out var userId))
                return Results.Unauthorized();

            var collection = await repository.GetById(id, cancellationToken);
            if (collection is null)
                return Results.NotFound();

            if (collection.OwnerId != userId)
                return Results.Forbid();

            collection.RemoveItem(quoteId);
            await repository.Update(collection, cancellationToken);

            return Results.Ok(collection);
        })
        .RequireAuthorization("can-edit-collections");
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out int userId)
    {
        var claim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out userId);
    }

    public record CreateCollectionRequest(string Name);

    public record AddCollectionItemRequest(int QuoteId);
}
