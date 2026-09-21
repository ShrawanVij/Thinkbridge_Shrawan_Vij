using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using QuotesApi.Outbox;

namespace QuotesApi.Messaging;

public class QuoteEventPublisher : IAsyncDisposable
{
    private readonly ServiceBusSender _sender;

    public QuoteEventPublisher(ServiceBusClient client, IOptions<ServiceBusOptions> options)
    {
        _sender = client.CreateSender(options.Value.TopicName);
    }

    public Task PublishQuoteCreatedAsync(QuoteCreatedEvent quoteCreated, CancellationToken cancellationToken)
    {
        var message = new ServiceBusMessage(JsonSerializer.Serialize(quoteCreated))
        {
            // Stable per-quote id: redelivering the same quote's event
            // keeps the same MessageId, which is what subscribers dedupe on.
            MessageId = $"quote-created-{quoteCreated.QuoteId}",
            ContentType = "application/json",
            Subject = nameof(QuoteCreatedEvent)
        };

        return _sender.SendMessageAsync(message, cancellationToken);
    }

    // Used by the outbox relay: MessageId is keyed to the outbox row's own
    // id, not the domain event, so re-publishing the same row after a crash
    // (before it was marked sent) always carries the same MessageId — the
    // consumer's existing idempotency check is what makes that safe to retry.
    public Task PublishOutboxMessageAsync(OutboxMessage outboxMessage, CancellationToken cancellationToken)
    {
        var message = new ServiceBusMessage(outboxMessage.Payload)
        {
            MessageId = $"outbox-{outboxMessage.Id}",
            ContentType = "application/json",
            Subject = outboxMessage.Type
        };

        return _sender.SendMessageAsync(message, cancellationToken);
    }

    // Publishes a message a handler can never successfully process, to
    // demonstrate the dead-letter queue catching it after MaxDeliveryCount.
    public Task PublishPoisonMessageAsync(CancellationToken cancellationToken)
    {
        var message = new ServiceBusMessage("this message can never be processed")
        {
            MessageId = $"poison-{Guid.NewGuid()}",
            Subject = "PoisonTest"
        };
        message.ApplicationProperties["simulateFailure"] = true;

        return _sender.SendMessageAsync(message, cancellationToken);
    }

    public ValueTask DisposeAsync() => _sender.DisposeAsync();
}
