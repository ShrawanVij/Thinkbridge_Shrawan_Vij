using QuotesApi.Services;

namespace QuotesApi.Models;

public class CollectionItem
{
    public int QuoteId { get; private set; }

    public DateTime AddedAt { get; private set; }

    private CollectionItem()
    {
    }

    public CollectionItem(int quoteId, IClock clock)
    {
        QuoteId = quoteId;
        AddedAt = clock.UtcNow.UtcDateTime;
    }
}