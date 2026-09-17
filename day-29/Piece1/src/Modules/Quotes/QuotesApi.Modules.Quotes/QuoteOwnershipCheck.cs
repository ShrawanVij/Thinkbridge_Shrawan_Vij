using Microsoft.EntityFrameworkCore;
using QuotesApi.Contracts;
using QuotesApi.Modules.Quotes.Data;

namespace QuotesApi.Modules.Quotes;

// The Quotes module's answer to Identity's "who owns this quote" question —
// registered as QuotesApi.Contracts.IQuoteOwnershipCheck, so Identity's
// authorization handler never sees QuotesDbContext or the Quote entity.
public class QuoteOwnershipCheck : IQuoteOwnershipCheck
{
    private readonly QuotesDbContext _db;

    public QuoteOwnershipCheck(QuotesDbContext db)
    {
        _db = db;
    }

    public async Task<int?> GetOwnerUserIdAsync(int quoteId, CancellationToken cancellationToken)
    {
        var quote = await _db.Quotes
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == quoteId, cancellationToken);

        return quote?.UserId;
    }
}
