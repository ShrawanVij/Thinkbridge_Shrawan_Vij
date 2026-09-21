using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuotesApi.Modules.Engagement.Data;
using QuotesApi.Modules.Engagement.Jobs;
using QuotesApi.Modules.Engagement.Messaging;

namespace QuotesApi.Modules.Engagement;

public static class EngagementModule
{
    public static IServiceCollection AddEngagementModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<EngagementDbContext>(options =>
        {
            var dbPath = OperatingSystem.IsWindows()
                ? Path.Combine(AppContext.BaseDirectory, "engagement.db")
                : "/tmp/engagement.db";

            options.UseSqlite(configuration.GetConnectionString("Engagement") ?? $"Data Source={dbPath}");
        });

        services.TryAddSingleton(_ => new ServiceBusClient(
            configuration.GetConnectionString("ServiceBus")
                ?? throw new InvalidOperationException("ConnectionStrings:ServiceBus is not configured.")));

        services.AddSingleton<EmailQueue>();
        services.AddHostedService<EmailWorker>();
        services.AddHostedService<NotifySubscriptionWorker>();
        services.AddHostedService<AuditSubscriptionWorker>();

        return services;
    }
}
