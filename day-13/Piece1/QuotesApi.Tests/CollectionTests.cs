using QuotesApi.Models;
using QuotesApi.Services;

namespace QuotesApi.Tests;

public class CollectionTests
{
    private readonly IClock _clock = new FakeClock();

    [Fact]
    public void RejectsNameShorterThan3Characters()
    {
        Assert.Throws<ArgumentException>(
            () => new Collection(1, "AB"));
    }

    [Fact]
    public void RejectsNameLongerThan80Characters()
    {
        var name = new string('A', 81);

        Assert.Throws<ArgumentException>(
            () => new Collection(1, name));
    }

    [Fact]
    public void RejectsDuplicateQuote()
    {
        var collection = new Collection(1, "My Quotes");

        collection.AddItem(10, _clock);

        Assert.Throws<InvalidOperationException>(
            () => collection.AddItem(10, _clock));
    }

    [Fact]
    public void RejectsMoreThan50Items()
    {
        var collection = new Collection(1, "My Quotes");

        for (var i = 1; i <= 50; i++)
            collection.AddItem(i, _clock);

        Assert.Throws<InvalidOperationException>(
            () => collection.AddItem(51, _clock));
    }

    [Fact]
    public void RemovesQuote()
    {
        var collection = new Collection(1, "My Quotes");

        collection.AddItem(10, _clock);
        collection.RemoveItem(10);

        Assert.Empty(collection.Items);
    }
}