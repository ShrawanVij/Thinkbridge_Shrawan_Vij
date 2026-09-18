using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using QuotesApi.Modules.Engagement.Data;

namespace QuotesApi.Modules.Engagement;

// Engagement previously wrote audit logs and never let anyone read them back
// — a real gap, since Day 26's whole point elsewhere was proving the
// pipeline actually did something observable end to end.
public static class EngagementEndpoints
{
    private const int MaxPageSize = 100;

    public static IEndpointRouteBuilder MapEngagementEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/engagement/audit-logs", async (
            EngagementDbContext db,
            CancellationToken cancellationToken,
            int? page,
            int? size) =>
        {
            var currentPage = page ?? 1;
            var currentSize = size ?? 20;

            if (currentPage < 1 || currentSize < 1 || currentSize > MaxPageSize)
            {
                return Results.BadRequest($"Page must be >= 1 and size must be between 1 and {MaxPageSize}.");
            }

            var logs = await db.AuditLogs
                .OrderByDescending(a => a.LoggedAt)
                .Skip((currentPage - 1) * currentSize)
                .Take(currentSize)
                .ToListAsync(cancellationToken);

            return Results.Ok(logs);
        })
        .RequireAuthorization();

        return app;
    }
}
