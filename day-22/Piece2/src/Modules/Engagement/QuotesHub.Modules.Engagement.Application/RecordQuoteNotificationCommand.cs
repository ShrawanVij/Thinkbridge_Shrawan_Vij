using MediatR;
using QuotesHub.Modules.Engagement.Domain;

namespace QuotesHub.Modules.Engagement.Application;

// What the consumer (Infrastructure) calls for every QuoteCreated message
// it receives. MessageId is the idempotency key — same pattern proven on
// Day 19's notify-sub/audit-sub.
public record RecordQuoteNotificationCommand(string MessageId, Guid QuoteId, string QuoteAuthor) : IRequest;

public class RecordQuoteNotificationCommandHandler(INotificationRepository repository) : IRequestHandler<RecordQuoteNotificationCommand>
{
    public async Task Handle(RecordQuoteNotificationCommand request, CancellationToken cancellationToken)
    {
        if (await repository.ExistsForMessageIdAsync(request.MessageId, cancellationToken))
        {
            return; // duplicate delivery — already recorded, no-op
        }

        var record = NotificationRecord.For(request.MessageId, request.QuoteId, request.QuoteAuthor, DateTime.UtcNow);
        await repository.AddAsync(record, cancellationToken);
    }
}
