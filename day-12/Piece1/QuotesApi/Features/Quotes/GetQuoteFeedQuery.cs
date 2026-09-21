using MediatR;
using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;

namespace QuotesApi.Features.Quotes;

public record GetQuoteFeedQuery(int Page, int Size) : IRequest<List<QuoteFeedItem>>;

public record QuoteFeedItem(int Id, string Author, string Text, DateTime CreatedAt, string Tags);

public class GetQuoteFeedQueryHandler(QuoteDbContext db) : IRequestHandler<GetQuoteFeedQuery, List<QuoteFeedItem>>
{
    public async Task<List<QuoteFeedItem>> Handle(GetQuoteFeedQuery request, CancellationToken cancellationToken)
    {
        return await db.Quotes
            .OrderByDescending(q => q.CreatedAt)
            .Skip((request.Page - 1) * request.Size)
            .Take(request.Size)
            .Select(q => new QuoteFeedItem(
                q.Id,
                q.Author,
                q.Text,
                q.CreatedAt,
                string.Join(", ", q.Tags.Select(t => t.Name))))
            .ToListAsync(cancellationToken);
    }
}
