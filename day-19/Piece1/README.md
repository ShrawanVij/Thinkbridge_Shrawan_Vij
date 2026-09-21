# Day 19 — Azure Service Bus Topics + DLQ

## Objective
Publish to a Service Bus topic with two subscriptions, consume with a competing-consumer worker, make handlers idempotent (dedupe on a message id), and demonstrate the dead-letter queue catching a poison message.

**Runs against the real Azure Service Bus SDK and wire protocol, but locally, for free** — via Microsoft's official Docker-based Service Bus Emulator (`servicebus-emulator/`) instead of a paid Azure namespace. The app code (`Azure.Messaging.ServiceBus`, connection string, topic/subscription names) is unchanged from what would target a real namespace — swapping the `ServiceBus:ConnectionString` in config to a real Azure connection string is the only change needed to go live.

---

## 1. The Publisher

`Messaging/QuoteEventPublisher.cs` — sends to the `quote-events` topic. Called from `CreateQuoteCommandHandler` after a quote is saved, so every real quote creation publishes a `QuoteCreated` event:

```csharp
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

    public ValueTask DisposeAsync() => _sender.DisposeAsync();
}
```

Two subscriptions on the topic (`notify-sub`, `audit-sub`) — declared in `servicebus-emulator/Config.json`, same as they'd be declared via `az servicebus topic subscription create` on real Azure. Every message published to the topic is delivered to **both**, independently.

---

## 2. The Consumer — Competing Consumers on `notify-sub`

`Messaging/NotifySubscriptionWorker.cs`:

```csharp
public class NotifySubscriptionWorker : BackgroundService
{
    private readonly IdempotencyStore _idempotencyStore = new();
    // ...

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processor = _client.CreateProcessor(_options.TopicName, _options.NotifySubscriptionName, new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = 4, // competing consumers: 4 handlers pulling from the same subscription
            AutoCompleteMessages = false
        });

        _processor.ProcessMessageAsync += HandleMessageAsync;
        _processor.ProcessErrorAsync += HandleErrorAsync;

        await _processor.StartProcessingAsync(stoppingToken);
        // ...
    }

    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        if (args.Message.ApplicationProperties.TryGetValue("simulateFailure", out var flag) && flag is true)
        {
            // Never completes: after MaxDeliveryCount attempts, the broker
            // dead-letters it automatically. No manual dead-lettering here.
            throw new InvalidOperationException("Simulated processing failure for poison-message test.");
        }

        if (!_idempotencyStore.TryMarkProcessed(args.Message.MessageId))
        {
            await args.CompleteMessageAsync(args.Message);
            return; // duplicate delivery — already handled, skip the work
        }

        var quoteCreated = JsonSerializer.Deserialize<QuoteCreatedEvent>(args.Message.Body.ToString());
        _logger.LogInformation("[notify-sub] Notifying about quote {QuoteId} by {Author}", quoteCreated?.QuoteId, quoteCreated?.Author);

        await args.CompleteMessageAsync(args.Message);
    }
}
```

`MaxConcurrentCalls = 4` is the competing-consumer piece: up to 4 message handlers run in parallel against `notify-sub`, each pulling the next available message — no two handlers ever get the same message, and adding more concurrency (or more instances of this process) increases throughput without any code change.

`AuditSubscriptionWorker` mirrors this on `audit-sub` with its own `IdempotencyStore` (dedup is scoped per-subscription — the same `MessageId` legitimately arrives at both subscriptions once each, and that's not a duplicate).

---

## 3. Idempotency Key Handling

`Messaging/IdempotencyStore.cs`:

```csharp
public class IdempotencyStore
{
    private readonly ConcurrentDictionary<string, byte> _processedMessageIds = new();

    public bool TryMarkProcessed(string messageId) =>
        _processedMessageIds.TryAdd(messageId, 0);
}
```

Dedup key is Service Bus's own `MessageId` — set deterministically by the publisher (`quote-created-{quoteId}`), so redelivering the same event (a publisher retry, or the SDK redelivering after a lock timeout) always carries the same id.

**Verified:** republished quote 1's event twice via `POST /events/quotes/1/republish` (same `MessageId` both times). First delivery processed normally; the second was skipped on both subscriptions:

```
[14:34:51 INF] [notify-sub] Notifying about quote 1 by Ada Lovelace
[14:34:51 INF] [audit-sub] Audit log: quote 1 created by Ada Lovelace at 09/01/2026 09:03:30
[14:35:03 INF] [notify-sub] Duplicate delivery of quote-created-1 — skipping, already processed
[14:35:03 INF] [audit-sub] Duplicate delivery of quote-created-1 — skipping, already processed
```

---

## 4. Proof: Poison Message → Dead-Letter Queue

`notify-sub`'s `MaxDeliveryCount` is set to `3` in `Config.json`. Triggered a message that always throws via `POST /events/quotes/poison-test`:

```
[14:35:10 WRN] [notify-sub] Poison message poison-b6c6037e-..., delivery attempt 3 — failing on purpose
[14:35:10 ERR] [notify-sub] Processor error
System.InvalidOperationException: Simulated processing failure for poison-message test.
```

After 3 failed delivery attempts, the broker stopped redelivering it — no manual dead-lettering call anywhere in the code, this is Service Bus's own `MaxDeliveryCount` behavior. Confirmed it actually landed in the DLQ by reading `notify-sub`'s dead-letter sub-queue via `GET /events/notify-sub/dead-letters`:

```json
[{
  "messageId": "poison-b6c6037e-05ae-48ce-98d3-4a571367ee56",
  "body": "this message can never be processed",
  "deliveryCount": 3,
  "deadLetterReason": "MaxDeliveryCountExceeded",
  "deadLetterErrorDescription": "Message could not be consumed after 3 delivery attempts."
}]
```

---

## 5. Running It

```
cd servicebus-emulator
docker compose up -d          # Service Bus Emulator + its SQL Edge metadata store
cd ../QuotesApi
dotnet run --urls http://localhost:5299
```

Emulator connection string (Development, `UseDevelopmentEmulator=true` — a fixed Microsoft-documented placeholder, not a real credential):
```
Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;
```

To point at a real Azure Service Bus namespace instead: create the namespace + topic + 2 subscriptions (Standard tier minimum — Basic tier doesn't support topics), and replace `ServiceBus:ConnectionString` in config with the real namespace connection string. No application code changes needed.
