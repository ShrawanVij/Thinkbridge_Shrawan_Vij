using System.Text.Json;
using MediatR;
using QuotesApi.Data;
using QuotesApi.Messaging;
using QuotesApi.Models;
using QuotesApi.Outbox;

namespace QuotesApi.Features.Quotes;

public record CreateQuoteRequest(string Author, string Text);

public record CreateQuoteCommand(string Author, string Text, int UserId) : IRequest<CreateQuoteResult>;

public record CreateQuoteResult(int Id, string Author, string Text, int UserId, DateTime CreatedAt);

public class QuoteValidationException(IDictionary<string, string[]> errors) : Exception
{
    public IDictionary<string, string[]> Errors { get; } = errors;
}

public class CreateQuoteCommandHandler(QuoteDbContext db) : IRequestHandler<CreateQuoteCommand, CreateQuoteResult>
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

        // Domain write + outbox row in one transaction: either both land or
        // neither does. The relay (a separate process step) is what actually
        // publishes — so a crash here can never leave a saved quote with no
        // record that an event needs to go out.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        db.Quotes.Add(quote);
        await db.SaveChangesAsync(cancellationToken);

        var quoteCreated = new QuoteCreatedEvent(quote.Id, quote.Author, quote.Text, quote.CreatedAt);
        db.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = nameof(QuoteCreatedEvent),
            Payload = JsonSerializer.Serialize(quoteCreated),
            OccurredAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new CreateQuoteResult(quote.Id, quote.Author, quote.Text, quote.UserId, quote.CreatedAt);
    }
}
