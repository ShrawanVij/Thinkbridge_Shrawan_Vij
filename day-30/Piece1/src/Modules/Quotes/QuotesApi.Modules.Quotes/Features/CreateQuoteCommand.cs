using MediatR;
using QuotesApi.Contracts;
using QuotesApi.Modules.Quotes.Data;
using QuotesApi.Modules.Quotes.Models;
using QuotesApi.Modules.Quotes.Outbox;
using System.Text.Json;

namespace QuotesApi.Modules.Quotes.Features;

public record CreateQuoteRequest(string Author, string Text);

public record CreateQuoteCommand(string Author, string Text, int UserId) : IRequest<CreateQuoteResult>;

public record CreateQuoteResult(int Id, string Author, string Text, int UserId, DateTime CreatedAt);

public class QuoteValidationException(IDictionary<string, string[]> errors) : Exception
{
    public IDictionary<string, string[]> Errors { get; } = errors;
}

public class CreateQuoteCommandHandler(QuotesDbContext db) : IRequestHandler<CreateQuoteCommand, CreateQuoteResult>
{
    public async Task<CreateQuoteResult> Handle(CreateQuoteCommand request, CancellationToken cancellationToken)
    {
        var errors = QuoteValidation.Validate(request.Author, request.Text);
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
        // neither does. The relay (a separate process) is what actually
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

// Shared by create and update — the exact same author/text rules both
// endpoints have always enforced, now written once instead of copy-pasted
// into the new edit handler.
internal static class QuoteValidation
{
    public static Dictionary<string, string[]> Validate(string author, string text)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(author))
        {
            errors["author"] = ["Author is required."];
        }
        else if (author.Length > 100)
        {
            errors["author"] = ["Author cannot exceed 100 characters."];
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            errors["text"] = ["Text is required."];
        }
        else if (text.Length > 1000)
        {
            errors["text"] = ["Text cannot exceed 1000 characters."];
        }

        return errors;
    }
}
