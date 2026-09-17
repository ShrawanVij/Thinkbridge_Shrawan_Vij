namespace QuotesApi.Contracts;

// What Identity's "can this user modify this quote" authorization handler
// needs from Quotes — nothing else. Implemented inside the Quotes module and
// registered in DI as this interface, so Identity never references the
// Quotes module's project (its DbContext, its EF entity) directly to answer
// a question that's really "does this quote belong to this user".
public interface IQuoteOwnershipCheck
{
    Task<int?> GetOwnerUserIdAsync(int quoteId, CancellationToken cancellationToken);
}
