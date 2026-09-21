using QuotesHub.Modules.Quotes.Domain;

namespace QuotesHub.Modules.Quotes.Application;

// Defined here (Application), implemented in Infrastructure — the
// dependency points inward, so Application never references EF Core,
// Service Bus, or any other concrete technology.
public interface IQuoteRepository
{
    Task AddAsync(Quote quote, CancellationToken cancellationToken);
    Task<Quote?> GetByIdAsync(QuoteId id, CancellationToken cancellationToken);
}
