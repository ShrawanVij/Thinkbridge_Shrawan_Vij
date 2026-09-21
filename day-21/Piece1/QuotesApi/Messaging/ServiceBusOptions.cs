namespace QuotesApi.Messaging;

public class ServiceBusOptions
{
    public string ConnectionString { get; set; } = "";
    public string TopicName { get; set; } = "quote-events";
    public string NotifySubscriptionName { get; set; } = "notify-sub";
    public string AuditSubscriptionName { get; set; } = "audit-sub";
}
