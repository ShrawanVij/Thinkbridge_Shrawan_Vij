namespace QuotesApi.Contracts;

// The only thing Engagement knows about a quote being created — published by
// Quotes onto the outbox/Service Bus, consumed by Engagement. Neither module
// references the other's project; this record is the entire boundary.
public record QuoteCreatedEvent(int QuoteId, string Author, string Text, DateTime CreatedAt);
