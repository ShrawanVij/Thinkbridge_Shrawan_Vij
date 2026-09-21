using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuotesHub.Modules.Engagement.Application;

namespace QuotesHub.Modules.Engagement.Infrastructure;

public static class EngagementModule
{
    public static IServiceCollection AddEngagementModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<INotificationRepository, InMemoryNotificationRepository>();
        services.AddHostedService<QuoteCreatedConsumer>();
        return services;
    }
}
