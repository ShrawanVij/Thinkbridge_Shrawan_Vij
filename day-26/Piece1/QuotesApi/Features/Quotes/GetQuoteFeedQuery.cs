using MediatR;
using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;

namespace QuotesApi.Features.Quotes;

public enum QuoteSortOrder
{
    NewestFirst,
    OldestFirst,
    AuthorAsc,
}

public record GetQuoteFeedQuery(int Page, int? Size, QuoteSortOrder SortOrder = QuoteSortOrder.NewestFirst) : IRequest<List<QuoteFeedItem>>;

public record QuoteFeedItem(int Id, string Author, string Text, DateTime CreatedAt, string Tags);

public class GetQuoteFeedQueryHandler(QuoteDbContext db) : IRequestHandler<GetQuoteFeedQuery, List<QuoteFeedItem>>
{
    public async Task<List<QuoteFeedItem>> Handle(GetQuoteFeedQuery request, CancellationToken cancellationToken)
    {
        IQueryable<Models.Quote> query = request.SortOrder switch
        {
            QuoteSortOrder.OldestFirst => db.Quotes.OrderBy(q => q.CreatedAt),
            QuoteSortOrder.AuthorAsc => db.Quotes.OrderBy(q => q.Author).ThenByDescending(q => q.CreatedAt),
            _ => db.Quotes.OrderByDescending(q => q.CreatedAt),
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