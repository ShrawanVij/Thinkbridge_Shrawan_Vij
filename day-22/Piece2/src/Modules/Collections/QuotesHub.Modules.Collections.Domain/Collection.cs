using QuotesHub.SharedKernel;

namespace QuotesHub.Modules.Collections.Domain;

// References a Quote by id only (QuoteRef), never by object — Collections
// doesn't reference the Quotes module's Domain assembly at all. If a
// referenced quote is deleted, that's a concern this module has to handle
// explicitly (e.g. reacting to a QuoteDeleted integration event), not
// something the type system can hide.
public class Collection : AggregateRoot<CollectionId>
{
    private readonly List<QuoteRef> _items = [];

    public string Name { get; private set; } = "";
    public Guid OwnerId { get; private set; }
    public IReadOnlyList<QuoteRef> Items => _items.AsReadOnly();

    private Collection() { }

    public static Collection Create(string name, Guid ownerId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Collection name is required.", nameof(name));
        }

        return new Collection { Id = CollectionId.New(), Name = name, OwnerId = ownerId };
    }

    public void AddQuote(QuoteRef quote)
    {
        if (!_items.Contains(quote))
        {
            _items.Add(quote);
        }
    }
}

public readonly record struct QuoteRef(Guid Value);
