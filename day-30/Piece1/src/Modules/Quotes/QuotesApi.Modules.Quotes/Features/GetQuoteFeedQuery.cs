using MediatR;
using Microsoft.EntityFrameworkCore;
using QuotesApi.Modules.Quotes.Data;

namespace QuotesApi.Modules.Quotes.Features;

public enum QuoteSortOrder
{
    NewestFirst,
    OldestFirst,
    AuthorAsc,
}

// Search and MyQuotesUserId are applied server-side, before paging — the
// frontend used to fetch one page and filter it client-side, which silently
// missed any match outside whatever page happened to load.
public record GetQuoteFeedQuery(
    int Page,
    int? Size,
    QuoteSortOrder SortOrder = QuoteSortOrder.NewestFirst,
    string? Search = null,
    int? MyQuotesUserId = null) : IRequest<List<QuoteFeedItem>>;

public record QuoteFeedItem(int Id, string Author, string Text, DateTime CreatedAt, string Tags);

public class GetQuoteFeedQueryHandler(QuotesDbContext db) : IRequestHandler<GetQuoteFeedQuery, List<QuoteFeedItem>>
{
    public async Task<List<QuoteFeedItem>> Handle(GetQuoteFeedQuery request, CancellationToken cancellationToken)
    {
        IQueryable<Models.Quote> query = db.Quotes;

        if (request.MyQuotesUserId is { } userId)
        {
            query = query.Where(q => q.UserId == userId);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(q => EF.Functions.Like(q.Author, $"%{term}%") || EF.Functions.Like(q.Text, $"%{term}%"));
        }

        query = request.SortOrder switch
        {
            QuoteSortOrder.OldestFirst => query.OrderBy(q => q.CreatedAt),
            QuoteSortOrder.AuthorAsc => query.OrderBy(q => q.Author).ThenByDescending(q => q.CreatedAt),
            _ => query.OrderByDescending(q => q.CreatedAt),
        };

        if (request.Size is { } size)
        {
            query = query.Skip((request.Page - 1) * size).Take(size);
        }

        return await query
            .Select(q => new QuoteFeedItem(
                q.Id,
                q.Author,
                q.Text,
                q.CreatedAt,
                string.Join(", ", q.Tags.Select(t => t.Name))))
            .ToListAsync(cancellationToken);
    }
}
