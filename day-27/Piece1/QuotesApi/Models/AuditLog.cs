namespace QuotesApi.Models;

// Written by AuditSubscriptionWorker when it processes a QuoteCreated event — the "worker
// touches the DB" leg of the API -> worker -> DB trace this day's exercise is about. Previously
// the audit subscriber only logged to Serilog; nothing was persisted.
public class AuditLog
{
    public int Id { get; set; }
    public int QuoteId { get; set; }
    public string Author { get; set; } = string.Empty;
    public DateTime QuoteCreatedAt { get; set; }
    public DateTime LoggedAt { get; set; }
}
