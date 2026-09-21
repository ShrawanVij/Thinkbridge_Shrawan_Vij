using QuotesHub.SharedKernel;

namespace QuotesHub.Modules.Quotes.Domain;

public record QuoteCreatedDomainEvent(
    QuoteId QuoteId,
    string Author,
    string Text,
    AuthorId AuthoredBy,
    DateTime OccurredAt) : IDomainEvent;
