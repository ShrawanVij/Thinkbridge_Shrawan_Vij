using MediatR;
using QuotesApi.Data;
using QuotesApi.Messaging;
using QuotesApi.Models;

namespace QuotesApi.Features.Quotes;

public record CreateQuoteRequest(string Author, string Text);

public record CreateQuoteCommand(string Author, string Text, int UserId) : IRequest<CreateQuoteResult>;

public record CreateQuoteResult(int Id, string Author, string Text, int UserId, DateTime CreatedAt);

public class QuoteValidationException(IDictionary<string, string[]> errors) : Exception
{
    public IDictionary<string, string[]> Errors { get; } = errors;
}

public class CreateQuoteCommandHandler(QuoteDbContext db, QuoteEventPublisher eventPublisher) : IRequestHandler<CreateQuoteCommand, CreateQuoteResult>
{
    public async Task<CreateQuoteResult> Handle(CreateQuoteCommand request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Author))
        {
            errors["author"] = ["Author is required."];
        }
        else if (request.Author.Length > 100)
        {
            errors["author"] = ["Author cannot exceed 100 characters."];
        }

        if (string.IsNullOrWhiteSpace(request.Text))
        {
            errors["text"] = ["Text is required."];
        }
        else if (request.Text.Length > 1000)
        {
            errors["text"] = ["Text cannot exceed 1000 characters."];
        }

        if (errors.Count > 0)
        {
            throw new QuoteValidationException(errors);
        }

        var quote = new Quote
        {
            Author = request.Author,
            Text = request.Text,
            UserId = request.UserId,
            CreatedAt = DateTime.UtcNow
        };

        db.Quotes.Add(quote);
        await db.SaveChangesAsync(cancellationToken);

        await eventPublisher.PublishQuoteCreatedAsync(
            new QuoteCreatedEvent(quote.Id, quote.Author, quote.Text, quote.CreatedAt),
            cancellationToken);

        return new CreateQuoteResult(quote.Id, quote.Author, quote.Text, quote.UserId, quote.CreatedAt);
    }
}
