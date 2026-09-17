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
        app.MapPost("/collections", async (
            CreateCollectionRequest request,
            ICollectionRepository repository,
            CancellationToken cancellationToken) =>
        {
            var collection = new Collection(request.OwnerId, request.Name);
            await repository.Add(collection, cancellationToken);
            return Results.Ok(collection);
        })
        .RequireAuthorization("can-edit-collections");

        app.MapPost("/collections/{id:int}/items", async (
            int id,
            AddCollectionItemRequest request,
            ICollectionRepository repository,
            IClock clock,
            CancellationToken cancellationToken) =>
        {
            var collection = await repository.GetById(id, cancellationToken);
            if (collection is null)
                return Results.NotFound();

            collection.AddItem(request.QuoteId, clock);
            await repository.Update(collection, cancellationToken);

            return Results.Ok(collection);
        })
        .RequireAuthorization("can-edit-collections");

        app.MapDelete("/collections/{id:int}/items/{quoteId:int}", async (
            int id,
            int quoteId,
            ICollectionRepository repository,
            CancellationToken cancellationToken) =>
        {
            var collection = await repository.GetById(id, cancellationToken);
            if (collection is null)
                return Results.NotFound();

            collection.RemoveItem(quoteId);
            await repository.Update(collection, cancellationToken);

            return Results.Ok(collection);
        })
        .RequireAuthorization("can-edit-collections");
    }

    public record CreateCollectionRequest(int OwnerId, string Name);

    public record AddCollectionItemRequest(int QuoteId);
}
