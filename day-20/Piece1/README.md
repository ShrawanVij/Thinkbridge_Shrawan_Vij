# Day 20 — The Outbox Pattern

## Objective
A DB write and a queue publish must not diverge. Write the domain change + an outbox row in one EF transaction, then a relay publishes and marks sent. Prove no message is lost if the publish step crashes.

Reuseing Day 19's `quote-events` topic and Service Bus Emulator — same infra, but now `CreateQuoteCommandHandler` no longer publishes directly; it only ever writes to the database.

---

## 1. The Outbox Table

`Outbox/OutboxMessage.cs`:

```csharp
public class OutboxMessage
{
    public Guid Id { get; set; }
    public string Type { get; set; } = "";
    public string Payload { get; set; } = "";
    public DateTime OccurredAt { get; set; }
    public DateTime? SentAt { get; set; }
}
```

`SentAt == null` is the only state that matters: unsent. Written in the same transaction as the domain row, `CreateQuoteCommandHandler.cs`:

```csharp
await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

db.Quotes.Add(quote);
await db.SaveChangesAsync(cancellationToken);

var quoteCreated = new QuoteCreatedEvent(quote.Id, quote.Author, quote.Text, quote.CreatedAt);
db.OutboxMessages.Add(new OutboxMessage
{
    Id = Guid.NewGuid(),
    Type = nameof(QuoteCreatedEvent),
    Payload = JsonSerializer.Serialize(quoteCreated),
    OccurredAt = DateTime.UtcNow
});
await db.SaveChangesAsync(cancellationToken);

await transaction.CommitAsync(cancellationToken);
```

Either both rows land or neither does — there's no way for a saved quote to exist without a corresponding outbox row, and no publish call anywhere in this handler that could fail independently of the DB write.

---

## 2. The Relay

`Outbox/OutboxRelay.cs` — a `BackgroundService` polling every 2 seconds. Publish and "mark sent" are two separate steps on purpose:

```csharp
var pending = await db.OutboxMessages
    .Where(m => m.SentAt == null)
    .OrderBy(m => m.OccurredAt)
    .ToListAsync(cancellationToken);

foreach (var message in pending)
{
    await publisher.PublishOutboxMessageAsync(message, cancellationToken);
    _logger.LogInformation("[outbox-relay] Published {Type} for outbox row {Id}", message.Type, message.Id);

    message.SentAt = DateTime.UtcNow;
    await db.SaveChangesAsync(cancellationToken);
}
```

`PublishOutboxMessageAsync` keys the Service Bus `MessageId` to the **outbox row's own id** (`outbox-{id}`), not a new guid per attempt — so retrying the same unsent row after a crash always re-sends under the same id, which is what the consumer's existing idempotency check (Day 19) dedupes on.

---

## 3. The Crash Scenario Tested

Between "publish succeeded" and "row marked sent" is exactly the window a real process crash lands in. Simulated it with a one-shot hook (`OutboxCrashSimulator`) that throws right there, armed via `POST /outbox/relay/crash-once`.

**Test:** armed the crash, then created a quote (`POST /cqrs/quotes`). Real logs:

```
[09:53:06 INF] [outbox-relay] Published QuoteCreatedEvent for outbox row 4e87cf33-...
[09:53:06 ERR] [outbox-relay] Simulated crash after publish, before marking sent (row 4e87cf33-...)
[09:53:06 ERR] Outbox relay tick failed; will retry on the next poll
[09:53:06 INF] [notify-sub] Notifying about quote 1 by Grace Hopper
[09:53:06 INF] [audit-sub] Audit log: quote 1 created by Grace Hopper at 09/02/2026 04:23:03

[09:53:08 INF] [outbox-relay] Published QuoteCreatedEvent for outbox row 4e87cf33-...
[09:53:08 INF] [notify-sub] Duplicate delivery of outbox-4e87cf33-... — skipping, already processed
[09:53:08 INF] [audit-sub] Duplicate delivery of outbox-4e87cf33-... — skipping, already processed
```

Final DB state (`GET /outbox/messages`) — the row ends up `SentAt`-marked despite the crash:
```json
[{"id":"4e87cf33-...","type":"QuoteCreatedEvent","occurredAt":"...04:23:03","sentAt":"...04:23:08"}]
```

---

## 4. Why No Message Is Lost or Duplicated

**Not lost:** the crash happens *after* the message already reached the broker — the relay only throws once the publish call has returned successfully. The outbox row stays `SentAt == null` because the crash pre-empted that update, so the very next poll (2s later) finds it still pending and republishes it. Nothing about the domain write or the outbox row depends on the relay surviving; they were already durably committed in step 1.

**Not duplicated (in effect):** the broker *did* deliver the message twice — once before the crash, once on retry — because this is at-least-once delivery, not exactly-once. What prevents that from being double-processed is entirely on the consumer side: `IdempotencyStore.TryMarkProcessed(messageId)` on both `notify-sub` and `audit-sub` (Day 19) rejects the second delivery of the same `MessageId`, logged above as "Duplicate delivery ... skipping." The pattern's actual guarantee is **at-least-once delivery + idempotent consumer = effectively-once processing** — not that duplicates never happen at the transport level, but that they never matter once they arrive.
