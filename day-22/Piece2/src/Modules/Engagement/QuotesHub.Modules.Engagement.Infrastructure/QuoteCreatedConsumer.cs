using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace QuotesHub.Modules.Engagement.Infrastructure;

// Scaffolded, not wired to a live broker yet — this is where the
// Week-1 API's already-proven pattern (Service Bus topic subscription,
// competing consumers via MaxConcurrentCalls, IdempotencyStore keyed on
// MessageId, from Day 19-21) plugs in: subscribe to quote-events'
// engagement-sub, deserialize each QuoteCreated payload, and dispatch
// RecordQuoteNotificationCommand(message.MessageId, ...) through MediatR.
// Left as a no-op BackgroundService here so the kickoff builds and runs
// without requiring a live Service Bus namespace/emulator.
public class QuoteCreatedConsumer(ILogger<QuoteCreatedConsumer> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "[engagement] QuoteCreatedConsumer scaffolded — see class remarks for the wiring plan");
        return Task.CompletedTask;
    }
}
