using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;

namespace QuotesApi.Messaging;

// Second subscription on the same topic — every QuoteCreated event
// published once is delivered independently here AND to notify-sub.
// This is the fan-out half of pub/sub; NotifySubscriptionWorker's
// MaxConcurrentCalls is the competing-consumer half, within one subscription.
public class AuditSubscriptionWorker : BackgroundService
{
    private readonly ServiceBusClient _client;
    // Owned per-worker, not shared with NotifySubscriptionWorker's store —
    // see the comment on that field for why.
    private readonly IdempotencyStore _idempotencyStore = new();
    private readonly ServiceBusOptions _options;
    private readonly ILogger<AuditSubscriptionWorker> _logger;
    private ServiceBusProcessor? _processor;

    public AuditSubscriptionWorker(
        ServiceBusClient client,
        IOptions<ServiceBusOptions> options,
        ILogger<AuditSubscriptionWorker> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processor = _client.CreateProcessor(_options.TopicName, _options.AuditSubscriptionName, new ServiceBusProcessorOptions
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
            _logger.LogInformation("[audit-sub] Duplicate delivery of {MessageId} — skipping, already processed", args.Message.MessageId);
            await args.CompleteMessageAsync(args.Message);
            return;
        }

        var quoteCreated = JsonSerializer.Deserialize<QuoteCreatedEvent>(args.Message.Body.ToString());
        _logger.LogInformation(
            "[audit-sub] Audit log: quote {QuoteId} created by {Author} at {CreatedAt}",
            quoteCreated?.QuoteId, quoteCreated?.Author, quoteCreated?.CreatedAt);

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
