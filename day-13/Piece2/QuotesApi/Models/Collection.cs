using QuotesApi.Services;

namespace QuotesApi.Models;

public class Collection
{
    public int Id { get; private set; }

    public string Name { get; private set; }

    public int OwnerId { get; private set; }

    public List<CollectionItem> Items { get; private set; } = new();

    private Collection()
    {
        Name = string.Empty;
    }

    public Collection(int ownerId, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length < 3 || name.Length > 80)
            throw new ArgumentException("Collection name must be between 3 and 80 characters.");

        OwnerId = ownerId;
        Name = name;
    }

    public void AddItem(int quoteId, IClock clock)
    {
        if (Items.Count >= 50)
            throw new InvalidOperationException("A collection cannot contain more than 50 items.");

        if (Items.Any(x => x.QuoteId == quoteId))
            throw new InvalidOperationException("Quote already exists in the collection.");

        Items.Add(new CollectionItem(quoteId, clock));
    }

    public void RemoveItem(int quoteId)
    {
        var item = Items.FirstOrDefault(x => x.QuoteId == quoteId);

        if (item is null)
            throw new KeyNotFoundException("Quote not found in the collection.");

        Items.Remove(item);
    }
}