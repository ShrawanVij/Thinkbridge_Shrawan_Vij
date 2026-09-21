namespace QuotesHub.Modules.Quotes.Domain;

public readonly record struct Tag
{
    public string Value { get; }

    public Tag(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Tag cannot be empty.", nameof(value));
        }

        Value = value.Trim().ToLowerInvariant();
    }

    public override string ToString() => Value;
}
