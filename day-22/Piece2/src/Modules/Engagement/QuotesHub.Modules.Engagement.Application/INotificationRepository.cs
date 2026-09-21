using QuotesHub.Modules.Engagement.Domain;

namespace QuotesHub.Modules.Engagement.Application;

public interface INotificationRepository
{
    Task AddAsync(NotificationRecord record, CancellationToken cancellationToken);
    Task<bool> ExistsForMessageIdAsync(string messageId, CancellationToken cancellationToken);
}
