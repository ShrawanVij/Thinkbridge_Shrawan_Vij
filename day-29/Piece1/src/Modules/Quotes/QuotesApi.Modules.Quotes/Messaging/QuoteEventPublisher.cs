using Azure.Messaging.ServiceBus;
using QuotesApi.Modules.Quotes.Outbox;

namespace QuotesApi.Modules.Quotes.Messaging;

public class QuoteEventPublisher : IAsyncDisposable
{
    private readonly ServiceBusSender _sender;

    public QuoteEventPublisher(ServiceBusClient client)
    {
        _sender = client.CreateSender("quote-events");
    }

    // Used by the outbox relay: MessageId is keyed to the outbox row's own
    // id, so re-publishing the same row after a crash (before it was marked
    // sent) always carries the same MessageId — a subscriber's idempotency
    // check is what makes that safe to retry.
    public Task PublishAsync(OutboxMessage outboxMessage, CancellationToken cancellationToken)
    {
        var message = new ServiceBusMessage(outboxMessage.Payload)
        {
            MessageId = $"outbox-{outboxMessage.Id}",
            ContentType = "application/json",
            Subject = outboxMessage.Type
        };

        return _sender.SendMessageAsync(message, cancellationToken);
    }

    public ValueTask DisposeAsync() => _sender.DisposeAsync();
}
