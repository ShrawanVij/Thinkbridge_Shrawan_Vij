namespace QuotesApi.Outbox;

public class OutboxMessage
{
    public Guid Id { get; set; }
    public string Type { get; set; } = "";
    public string Payload { get; set; } = "";
    public DateTime OccurredAt { get; set; }
    public DateTime? SentAt { get; set; }

    // W3C traceparent of the request that created this row (Activity.Current?.Id at write
    // time). The relay reads this back later — often seconds after the original request's
    // Activity has already ended — and adds it as a span Link rather than a parent, so the
    // async publish shows up as its own trace that's still traceable back to the request
    // that caused it, instead of either losing the connection or producing an artificially
    // long "request" span that spans the outbox poll delay.
    public string? TraceParent { get; set; }
}
