using System.Collections.Concurrent;

namespace QuotesApi.Modules.Engagement.Messaging;

// Dedupes on Service Bus's MessageId — a second delivery of the same
// message becomes a no-op instead of doing the work twice.
public class IdempotencyStore
{
    private readonly ConcurrentDictionary<string, byte> _processedMessageIds = new();

    public bool TryMarkProcessed(string messageId) =>
        _processedMessageIds.TryAdd(messageId, 0);
}
