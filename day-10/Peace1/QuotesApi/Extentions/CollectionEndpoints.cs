using QuotesApi.Models;
using QuotesApi.Repositories;
using QuotesApi.Services;

namespace QuotesApi.Extensions;

public static class CollectionEndpoints
{
    public static void MapCollectionEndpoints(this WebApplication app)
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

        app.MapPost("/collections/{id}/items", async (
            int id,
            AddCollectionItemRequest request,
            ICollectionRepository repository,
            IClock clock,
            CancellationToken cancellationToken) =>
        {
            var collection = await repository.GetById(
                id,
                cancellationToken);

            if (collection is null)
                return Results.NotFound();

            collection.AddItem(request.QuoteId, clock);

            await repository.Update(
                collection,
                cancellationToken);

            return Results.Ok(collection);
        })
        .RequireAuthorization("can-edit-collections");

        app.MapDelete("/collections/{id}/items/{quoteId}", async (
            int id,
            int quoteId,
            ICollectionRepository repository,
            CancellationToken cancellationToken) =>
        {
            var collection = await repository.GetById(
                id,
                cancellationToken);

            if (collection is null)
                return Results.NotFound();

            collection.RemoveItem(quoteId);

            await repository.Update(
                collection,
                cancellationToken);

            return Results.Ok(collection);
        })
        .RequireAuthorization("can-edit-collections");
    }

    public record CreateCollectionRequest(int OwnerId, string Name);

    public record AddCollectionItemRequest(int QuoteId);
}