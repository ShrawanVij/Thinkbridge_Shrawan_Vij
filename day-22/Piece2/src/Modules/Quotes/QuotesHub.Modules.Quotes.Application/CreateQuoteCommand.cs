using MediatR;
using QuotesHub.Modules.Quotes.Domain;

namespace QuotesHub.Modules.Quotes.Application;

public record CreateQuoteCommand(string Author, string Text, Guid AuthoredBy) : IRequest<CreateQuoteResult>;

public record CreateQuoteResult(Guid Id, string Author, string Text, DateTime CreatedAt);

public class CreateQuoteCommandHandler(IQuoteRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<CreateQuoteCommand, CreateQuoteResult>
{
    public async Task<CreateQuoteResult> Handle(CreateQuoteCommand request, CancellationToken cancellationToken)
    {
        var quote = Quote.Create(request.Author, request.Text, new AuthorId(request.AuthoredBy), DateTime.UtcNow);

        await repository.AddAsync(quote, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken); // domain change + outbox row, one transaction

        return new CreateQuoteResult(quote.Id.Value, quote.Author, quote.Text, quote.CreatedAt);
    }
}
