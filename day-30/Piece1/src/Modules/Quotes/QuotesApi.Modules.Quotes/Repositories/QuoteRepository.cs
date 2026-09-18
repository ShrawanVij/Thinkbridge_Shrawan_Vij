using Microsoft.EntityFrameworkCore;
using QuotesApi.Modules.Quotes.Data;
using QuotesApi.Modules.Quotes.Models;

namespace QuotesApi.Modules.Quotes.Repositories;

public class QuoteRepository : IQuoteRepository
{
    private readonly QuotesDbContext _db;

    public QuoteRepository(QuotesDbContext db)
    {
        _db = db;
    }

    public async Task<List<Quote>> GetQuotesAsync(int page, int size, CancellationToken cancellationToken)
    {
        return await _db.Quotes
            .AsNoTracking()
            .OrderBy(q => q.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);
    }

    public async Task<Quote?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        // Without this Include, Tags always comes back empty regardless of
        // what's actually attached — the detail page has been silently
        // showing no tags for every quote since this endpoint was written.
        return await _db.Quotes
            .Include(q => q.Tags)
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
    }

    public async Task<Quote> AddAsync(Quote quote, CancellationToken cancellationToken)
    {
        _db.Quotes.Add(quote);
        await _db.SaveChangesAsync(cancellationToken);

        return quote;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var quote = await _db.Quotes.FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

        if (quote is null)
            return false;

        _db.Quotes.Remove(quote);
        await _db.SaveChangesAsync(cancellationToken);

        return true;
    }
}
