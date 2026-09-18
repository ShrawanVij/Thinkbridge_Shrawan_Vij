namespace QuotesApi.Modules.Quotes.Outbox;

// Owned by the Quotes module only — Engagement never queries this table
// directly, it only ever sees what gets published to Service Bus from it.
public class OutboxMessage
{
    public Guid Id { get; set; }
    public string Type { get; set; } = "";
    public string Payload { get; set; } = "";
    public DateTime OccurredAt { get; set; }
    public DateTime? SentAt { get; set; }
}
