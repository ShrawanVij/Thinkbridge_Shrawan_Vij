using QuotesHub.SharedKernel;

namespace QuotesHub.Modules.Engagement.Domain;

// Engagement's own read-side record of "a quote was published" — built
// from the QuoteCreated integration event, not from a foreign key into the
// Quotes module's database. Deliberately duplicates a little data
// (QuoteAuthor) rather than reaching across the module boundary for it.
public class NotificationRecord : Entity<NotificationRecordId>
{
    public string MessageId { get; private set; } = "";
    public Guid QuoteId { get; private set; }
    public string QuoteAuthor { get; private set; } = "";
    public DateTime NotifiedAt { get; private set; }

    private NotificationRecord() { }

    public static NotificationRecord For(string messageId, Guid quoteId, string quoteAuthor, DateTime notifiedAt) =>
        new()
        {
            Id = NotificationRecordId.New(),
            MessageId = messageId,
            QuoteId = quoteId,
            QuoteAuthor = quoteAuthor,
            NotifiedAt = notifiedAt
        };
}

public readonly record struct NotificationRecordId(Guid Value)
{
    public static NotificationRecordId New() => new(Guid.NewGuid());
}
