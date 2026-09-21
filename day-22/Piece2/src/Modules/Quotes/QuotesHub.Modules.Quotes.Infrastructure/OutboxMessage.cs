namespace QuotesHub.Modules.Quotes.Infrastructure;

// Each module owns its own outbox table — not a shared one. Sharing an
// outbox across modules would be a hidden coupling point (a schema change
// for one module's events could break another's relay).
public class OutboxMessage
{
    public Guid Id { get; set; }
    public string Type { get; set; } = "";
    public string Payload { get; set; } = "";
    public DateTime OccurredAt { get; set; }
    public DateTime? SentAt { get; set; }
}
