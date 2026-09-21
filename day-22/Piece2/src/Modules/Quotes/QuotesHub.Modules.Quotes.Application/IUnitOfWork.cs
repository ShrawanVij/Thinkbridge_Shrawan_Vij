namespace QuotesHub.Modules.Quotes.Application;

// Implemented in Infrastructure as a thin wrapper over the module's own
// DbContext. SaveChangesAsync is where the outbox row gets written in the
// same transaction as the aggregate's changes (see the design doc's async
// flows section) — Application code never has to know that detail.
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
