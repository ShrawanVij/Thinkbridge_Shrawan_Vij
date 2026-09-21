using Microsoft.EntityFrameworkCore;
using QuotesHub.Modules.Quotes.Application;
using QuotesHub.Modules.Quotes.Domain;

namespace QuotesHub.Modules.Quotes.Infrastructure;

public class QuoteRepository(QuotesDbContext db) : IQuoteRepository
{
    public Task AddAsync(Quote quote, CancellationToken cancellationToken)
    {
        db.Quotes.Add(quote);
        return Task.CompletedTask;
    }

    public Task<Quote?> GetByIdAsync(QuoteId id, CancellationToken cancellationToken) =>
        db.Quotes.FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
}
