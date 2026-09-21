using QuotesHub.Modules.Quotes.Domain;
using Xunit;

namespace QuotesHub.Modules.Quotes.Domain.Tests;

public class QuoteTests
{
    private static readonly AuthorId SomeAuthor = new(Guid.NewGuid());

    [Fact]
    public void Create_WithValidData_RaisesQuoteCreatedDomainEvent()
    {
        var quote = Quote.Create("Ada Lovelace", "The Analytical Engine weaves algebraic patterns.", SomeAuthor, DateTime.UtcNow);

        var domainEvent = Assert.Single(quote.DomainEvents);
        var created = Assert.IsType<QuoteCreatedDomainEvent>(domainEvent);
        Assert.Equal(quote.Id, created.QuoteId);
    }

    [Fact]
    public void Create_WithEmptyText_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Quote.Create("Ada Lovelace", "", SomeAuthor, DateTime.UtcNow));
    }

    [Fact]
    public void Create_WithTextOverMaxLength_ThrowsArgumentException()
    {
        var tooLong = new string('x', Quote.MaxTextLength + 1);

        Assert.Throws<ArgumentException>(() => Quote.Create("Ada Lovelace", tooLong, SomeAuthor, DateTime.UtcNow));
    }

    [Fact]
    public void Tag_SameTagTwice_IsIdempotent()
    {
        var quote = Quote.Create("Ada Lovelace", "Text", SomeAuthor, DateTime.UtcNow);

        quote.Tag(new Tag("science"));
        quote.Tag(new Tag("Science")); // different casing, same normalized tag

        Assert.Single(quote.Tags);
    }

    [Fact]
    public void Tag_BeyondMaxTags_Throws()
    {
        var quote = Quote.Create("Ada Lovelace", "Text", SomeAuthor, DateTime.UtcNow);

        for (var i = 0; i < Quote.MaxTags; i++)
        {
            quote.Tag(new Tag($"tag{i}"));
        }

        Assert.Throws<InvalidOperationException>(() => quote.Tag(new Tag("one-too-many")));
    }
}
