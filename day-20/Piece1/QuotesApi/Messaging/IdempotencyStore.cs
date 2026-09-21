using System.Collections.Concurrent;

namespace QuotesApi.Messaging;

// Dedupes on Service Bus's MessageId. Redelivery (competing consumers,
// retries, at-least-once delivery) can hand the same message to a handler
// more than once — this makes a second delivery a no-op instead of doing
// the work twice.
public class IdempotencyStore
{
    private readonly ConcurrentDictionary<string, byte> _processedMessageIds = new();

    public bool TryMarkProcessed(string messageId) =>
        _processedMessageIds.TryAdd(messageId, 0);
}
