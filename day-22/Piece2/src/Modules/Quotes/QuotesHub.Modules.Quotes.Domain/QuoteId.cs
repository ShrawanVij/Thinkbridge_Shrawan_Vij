namespace QuotesHub.Modules.Quotes.Domain;

// Strongly-typed id: a bare Guid parameter can't tell a QuoteId from an
// AuthorId at the call site or in a method signature — this can.
public readonly record struct QuoteId(Guid Value)
{
    public static QuoteId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
