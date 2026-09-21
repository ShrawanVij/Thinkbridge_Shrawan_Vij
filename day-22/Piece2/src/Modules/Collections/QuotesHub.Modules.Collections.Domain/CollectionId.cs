namespace QuotesHub.Modules.Collections.Domain;

public readonly record struct CollectionId(Guid Value)
{
    public static CollectionId New() => new(Guid.NewGuid());
}
