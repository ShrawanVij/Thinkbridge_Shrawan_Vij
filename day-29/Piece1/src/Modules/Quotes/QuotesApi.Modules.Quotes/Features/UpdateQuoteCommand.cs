using MediatR;
using QuotesApi.Modules.Quotes.Data;

namespace QuotesApi.Modules.Quotes.Features;

public record UpdateQuoteRequest(string Author, string Text);

public record UpdateQuoteCommand(int Id, string Author, string Text) : IRequest<UpdateQuoteResult?>;

public record UpdateQuoteResult(int Id, string Author, string Text, int UserId, DateTime CreatedAt);

// Ownership is checked at the endpoint (the "can-edit-own-quote" policy,
// same shape as the existing delete check) before this handler ever runs —
// this handler only knows how to apply a valid edit to a quote that exists.
public class UpdateQuoteCommandHandler(QuotesDbContext db) : IRequestHandler<UpdateQuoteCommand, UpdateQuoteResult?>
{
    public async Task<UpdateQuoteResult?> Handle(UpdateQuoteCommand request, CancellationToken cancellationToken)
    {
        var errors = QuoteValidation.Validate(request.Author, request.Text);
        if (errors.Count > 0)
        {
            throw new QuoteValidationException(errors);
        }

        var quote = await db.Quotes.FindAsync([request.Id], cancellationToken);
        if (quote is null)
        {
            return null;
        }

        quote.Author = request.Author;
        quote.Text = request.Text;

        await db.SaveChangesAsync(cancellationToken);

        return new UpdateQuoteResult(quote.Id, quote.Author, quote.Text, quote.UserId, quote.CreatedAt);
    }
}
