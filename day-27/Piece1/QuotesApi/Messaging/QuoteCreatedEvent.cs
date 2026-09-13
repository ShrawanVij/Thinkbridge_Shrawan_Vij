namespace QuotesApi.Messaging;

public record QuoteCreatedEvent(int QuoteId, string Author, string Text, DateTime CreatedAt);
