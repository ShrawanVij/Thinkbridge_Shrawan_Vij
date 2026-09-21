using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Messaging;

namespace QuotesApi.Outbox;

// Polls for unsent outbox rows and publishes them. Publish and "mark sent"
// are two separate steps — if the process dies between them, the row is
// still unsent on the next poll and gets republished. That's at-least-once
// delivery by design; the consumer's idempotency check (Day 19) is what
// turns "at least once" into "effectively once" downstream.
public class OutboxRelay : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OutboxCrashSimulator _crashSimulator;
    private readonly ILogger<OutboxRelay> _logger;

    public OutboxRelay(IServiceScopeFactory scopeFactory, OutboxCrashSimulator crashSimulator, ILogger<OutboxRelay> logger)
    {
        _scopeFactory = scopeFactory;
        _crashSimulator = crashSimulator;
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
                _logger.LogError(ex, "Outbox relay tick failed; will retry on the next poll");
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
        var db = scope.ServiceProvider.GetRequiredService<QuoteDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<QuoteEventPublisher>();

        var pending = await db.OutboxMessages
            .Where(m => m.SentAt == null)
            .OrderBy(m => m.OccurredAt)
            .ToListAsync(cancellationToken);

        foreach (var message in pending)
        {
            await publisher.PublishOutboxMessageAsync(message, cancellationToken);
            _logger.LogInformation("[outbox-relay] Published {Type} for outbox row {Id}", message.Type, message.Id);

            if (_crashSimulator.TryConsumeCrash())
            {
                _logger.LogError("[outbox-relay] Simulated crash after publish, before marking sent (row {Id})", message.Id);
                throw new InvalidOperationException("Simulated relay crash for outbox test.");
            }

            message.SentAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
