namespace QuotesApi.Modules.Engagement.Models;

// Written when this module processes a QuoteCreated event — its own record
// that "a quote was published", not a foreign key into the Quotes module's
// database.
public class AuditLog
{
    public int Id { get; set; }
    public int QuoteId { get; set; }
    public string Author { get; set; } = string.Empty;
    public DateTime QuoteCreatedAt { get; set; }
    public DateTime LoggedAt { get; set; }
}
