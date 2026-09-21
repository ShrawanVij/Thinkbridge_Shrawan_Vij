using System.Collections.Concurrent;
using QuotesHub.Modules.Engagement.Application;
using QuotesHub.Modules.Engagement.Domain;

namespace QuotesHub.Modules.Engagement.Infrastructure;

public class InMemoryNotificationRepository : INotificationRepository
{
    private readonly ConcurrentDictionary<string, NotificationRecord> _byMessageId = new();

    public Task AddAsync(NotificationRecord record, CancellationToken cancellationToken)
    {
        _byMessageId[record.MessageId] = record;
        return Task.CompletedTask;
    }

    public Task<bool> ExistsForMessageIdAsync(string messageId, CancellationToken cancellationToken) =>
        Task.FromResult(_byMessageId.ContainsKey(messageId));
}
