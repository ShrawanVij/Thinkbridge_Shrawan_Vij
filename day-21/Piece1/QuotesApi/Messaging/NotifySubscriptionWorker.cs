using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;

namespace QuotesApi.Messaging;

// Competing-consumer worker: MaxConcurrentCalls lets several message
// handlers run in parallel against the same subscription, each pulling
// the next available message — no two handlers get the same message.
public class NotifySubscriptionWorker : BackgroundService
{
    private readonly ServiceBusClient _client;
    // Owned per-worker (not DI-shared): dedup is scoped to this subscription.
    // notify-sub and audit-sub each see the same MessageId independently, so
    // a shared store would wrongly treat the second subscription's delivery
    // as a duplicate of the first.
    private readonly IdempotencyStore _idempotencyStore = new();
    private readonly ServiceBusOptions _options;
    private readonly ILogger<NotifySubscriptionWorker> _logger;
    private ServiceBusProcessor? _processor;

    public NotifySubscriptionWorker(
        ServiceBusClient client,
        IOptions<ServiceBusOptions> options,
        ILogger<NotifySubscriptionWorker> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processor = _client.CreateProcessor(_options.TopicName, _options.NotifySubscriptionName, new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = 4,
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
        if (args.Message.ApplicationProperties.TryGetValue("simulateFailure", out var flag) && flag is true)
        {
            // Never completes: after MaxDeliveryCount attempts, the broker
            // dead-letters it automatically. No manual dead-lettering here.
            _logger.LogWarning(
                "[notify-sub] Poison message {MessageId}, delivery attempt {Count} — failing on purpose",
                args.Message.MessageId, args.Message.DeliveryCount);
            throw new InvalidOperationException("Simulated processing failure for poison-message test.");
        }

        if (!_idempotencyStore.TryMarkProcessed(args.Message.MessageId))
        {
            _logger.LogInformation("[notify-sub] Duplicate delivery of {MessageId} — skipping, already processed", args.Message.MessageId);
            await args.CompleteMessageAsync(args.Message);
            return;
        }

        var quoteCreated = JsonSerializer.Deserialize<QuoteCreatedEvent>(args.Message.Body.ToString());
        _logger.LogInformation(
            "[notify-sub] Notifying about quote {QuoteId} by {Author}",
            quoteCreated?.QuoteId, quoteCreated?.Author);

        await args.CompleteMessageAsync(args.Message);
    }

    private Task HandleErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception, "[notify-sub] Processor error");
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
