using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuotesApi.Modules.Quotes.Data;
using QuotesApi.Modules.Quotes.Messaging;

namespace QuotesApi.Modules.Quotes.Outbox;

// Polls for unsent outbox rows and publishes them. Publish and "mark sent"
// are two separate steps — at-least-once delivery by design (the Day 20
// pattern); a subscriber's own idempotency check on MessageId is what turns
// that into effectively-once downstream.
public class OutboxRelay : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxRelay> _logger;

    public OutboxRelay(IServiceScopeFactory scopeFactory, ILogger<OutboxRelay> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RelayPendingMessagesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "[quotes-outbox-relay] tick failed; will retry on the next poll");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // expected: stoppingToken was cancelled during shutdown
            }
        }
    }

    private async Task RelayPendingMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuotesDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<QuoteEventPublisher>();

        var pending = await db.OutboxMessages
            .Where(m => m.SentAt == null)
            .OrderBy(m => m.OccurredAt)
            .ToListAsync(cancellationToken);

        foreach (var message in pending)
        {
            await publisher.PublishAsync(message, cancellationToken);
            _logger.LogInformation("[quotes-outbox-relay] published {Type} for outbox row {Id}", message.Type, message.Id);

            message.SentAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
