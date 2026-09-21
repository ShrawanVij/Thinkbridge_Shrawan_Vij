using QuotesHub.SharedKernel;

namespace QuotesHub.Modules.Quotes.Domain;

// The core aggregate. Owns its own invariants — there is no code path,
// inside or outside this module, that can construct or mutate a Quote into
// an invalid state, because the only way to reach these fields is through
// these methods.
public class Quote : AggregateRoot<QuoteId>
{
    public const int MaxAuthorLength = 100;
    public const int MaxTextLength = 1000;
    public const int MaxTags = 10;

    private readonly List<Tag> _tags = [];

    public string Author { get; private set; } = "";
    public string Text { get; private set; } = "";
    public AuthorId AuthoredBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public IReadOnlyList<Tag> Tags => _tags.AsReadOnly();

    private Quote() { }

    public static Quote Create(string author, string text, AuthorId authoredBy, DateTime createdAt)
    {
        if (string.IsNullOrWhiteSpace(author))
        {
            throw new ArgumentException("Author is required.", nameof(author));
        }

        if (author.Length > MaxAuthorLength)
        {
            throw new ArgumentException($"Author cannot exceed {MaxAuthorLength} characters.", nameof(author));
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text is required.", nameof(text));
        }

        if (text.Length > MaxTextLength)
        {
            throw new ArgumentException($"Text cannot exceed {MaxTextLength} characters.", nameof(text));
        }

        var quote = new Quote
        {
            Id = QuoteId.New(),
            Author = author,
            Text = text,
            AuthoredBy = authoredBy,
            CreatedAt = createdAt
        };

        quote.Raise(new QuoteCreatedDomainEvent(quote.Id, quote.Author, quote.Text, quote.AuthoredBy, createdAt));

        return quote;
    }

    public void Tag(Tag tag)
    {
        if (_tags.Contains(tag))
        {
            return; // idempotent: tagging twice with the same tag is a no-op, not an error
        }

        if (_tags.Count >= MaxTags)
        {
            throw new InvalidOperationException($"A quote cannot carry more than {MaxTags} tags.");
        }

        _tags.Add(tag);
    }
}
