using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using QuotesApi.Modules.Quotes.Data;
using QuotesApi.Modules.Quotes.Models;
using System.Security.Claims;

namespace QuotesApi.Modules.Quotes;

public static class TagsEndpoints
{
    public static IEndpointRouteBuilder MapTagsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/tags", async (QuotesDbContext db, CancellationToken cancellationToken) =>
        {
            var tags = await db.Tags
                .OrderBy(t => t.Name)
                .Select(t => new { t.Id, t.Name })
                .ToListAsync(cancellationToken);

            return Results.Ok(tags);
        });

        // Attaching/detaching a tag is an edit of the quote, so it's gated
        // the same way editing the quote's own text is — same ownership
        // policy, same resource-based check.
        app.MapPost("/api/quotes/{id:int}/tags", async (
            int id,
            AddTagRequest request,
            IAuthorizationService authorizationService,
            ClaimsPrincipal user,
            QuotesDbContext db,
            CancellationToken cancellationToken) =>
        {
            var authorizationResult = await authorizationService.AuthorizeAsync(user, id, "can-edit-own-quote");
            if (!authorizationResult.Succeeded)
                return Results.Forbid();

            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["name"] = ["Tag name is required."] });

            var quote = await db.Quotes.Include(q => q.Tags).FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
            if (quote is null)
                return Results.NotFound();

            var name = request.Name.Trim();
            var tag = await db.Tags.FirstOrDefaultAsync(t => t.Name == name, cancellationToken);
            if (tag is null)
            {
                tag = new Tag { Name = name };
                db.Tags.Add(tag);
            }

            if (!quote.Tags.Any(t => t.Name == name))
            {
                quote.Tags.Add(tag);
            }

            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(quote.Tags.Select(t => new { t.Id, t.Name }));
        })
        .RequireAuthorization();

        app.MapDelete("/api/quotes/{id:int}/tags/{tagId:int}", async (
            int id,
            int tagId,
            IAuthorizationService authorizationService,
            ClaimsPrincipal user,
            QuotesDbContext db,
            CancellationToken cancellationToken) =>
        {
            var authorizationResult = await authorizationService.AuthorizeAsync(user, id, "can-edit-own-quote");
            if (!authorizationResult.Succeeded)
                return Results.Forbid();

            var quote = await db.Quotes.Include(q => q.Tags).FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
            if (quote is null)
                return Results.NotFound();

            var tag = quote.Tags.FirstOrDefault(t => t.Id == tagId);
            if (tag is null)
                return Results.NotFound();

            quote.Tags.Remove(tag);
            await db.SaveChangesAsync(cancellationToken);

            return Results.NoContent();
        })
        .RequireAuthorization();

        return app;
    }

    public record AddTagRequest(string Name);
}
