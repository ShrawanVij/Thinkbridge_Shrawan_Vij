using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuotesApi.Contracts;

namespace QuotesApi.Modules.Engagement.Messaging;

// Competing-consumer worker: MaxConcurrentCalls lets several handlers run
// in parallel against the same subscription.
public class NotifySubscriptionWorker : BackgroundService
{
    private readonly ServiceBusClient _client;
    private readonly IdempotencyStore _idempotencyStore = new();
    private readonly ILogger<NotifySubscriptionWorker> _logger;
    private ServiceBusProcessor? _processor;

    public NotifySubscriptionWorker(ServiceBusClient client, ILogger<NotifySubscriptionWorker> logger)
    {
        _client = client;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processor = _client.CreateProcessor("quote-events", "notify-sub", new ServiceBusProcessorOptions
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
        if (!_idempotencyStore.TryMarkProcessed(args.Message.MessageId))
        {
            _logger.LogInformation("[notify-sub] Duplicate delivery of {MessageId} — skipping", args.Message.MessageId);
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
