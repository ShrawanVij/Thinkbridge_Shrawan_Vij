using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuotesApi.Contracts;
using QuotesApi.Modules.Engagement.Data;
using QuotesApi.Modules.Engagement.Models;

namespace QuotesApi.Modules.Engagement.Messaging;

// Second subscription on the same topic — every QuoteCreated event is
// delivered independently here AND to notify-sub (fan-out), while
// MaxConcurrentCalls within each subscription is the competing-consumer half.
public class AuditSubscriptionWorker : BackgroundService
{
    private readonly ServiceBusClient _client;
    private readonly IdempotencyStore _idempotencyStore = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditSubscriptionWorker> _logger;
    private ServiceBusProcessor? _processor;

    public AuditSubscriptionWorker(
        ServiceBusClient client,
        IServiceScopeFactory scopeFactory,
        ILogger<AuditSubscriptionWorker> logger)
    {
        _client = client;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processor = _client.CreateProcessor("quote-events", "audit-sub", new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = 1,
            AutoCompleteMessages = false
        });

        _processor.ProcessMessageAsync += HandleMessageAsync;
        _processor.ProcessErrorAsync += HandleErrorAsync;

        await _processor.StartProcessingAsync(stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // expected: stoppingToken was cancelled during shutdown
        }

        await _processor.StopProcessingAsync(CancellationToken.None);
    }

    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        if (!_idempotencyStore.TryMarkProcessed(args.Message.MessageId))
        {
            _logger.LogInformation("[audit-sub] Duplicate delivery of {MessageId} — skipping", args.Message.MessageId);
            await args.CompleteMessageAsync(args.Message);
            return;
        }

        var quoteCreated = JsonSerializer.Deserialize<QuoteCreatedEvent>(args.Message.Body.ToString());

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EngagementDbContext>();
        db.AuditLogs.Add(new AuditLog
        {
            QuoteId = quoteCreated!.QuoteId,
            Author = quoteCreated.Author,
            QuoteCreatedAt = quoteCreated.CreatedAt,
            LoggedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(args.CancellationToken);

        await args.CompleteMessageAsync(args.Message);
    }

    private Task HandleErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception, "[audit-sub] Processor error");
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processor is not null)
        {
            await _processor.StopProcessingAsync(cancellationToken);
            await _processor.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }
}
