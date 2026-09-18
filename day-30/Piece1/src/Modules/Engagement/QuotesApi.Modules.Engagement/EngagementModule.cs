using Azure.Identity;
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
            var connectionString = configuration.GetConnectionString("Engagement");

            if (!string.IsNullOrEmpty(connectionString) && connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlServer(connectionString);
                return;
            }

            var dbPath = OperatingSystem.IsWindows()
                ? Path.Combine(AppContext.BaseDirectory, "engagement.db")
                : "/tmp/engagement.db";

            options.UseSqlite(connectionString ?? $"Data Source={dbPath}");
        });

        services.TryAddSingleton(_ =>
        {
            var serviceBusNamespace = configuration["ServiceBus:Namespace"];
            if (!string.IsNullOrEmpty(serviceBusNamespace))
            {
                return new ServiceBusClient(serviceBusNamespace, new ManagedIdentityCredential(clientId: configuration["AZURE_CLIENT_ID"]));
            }

            var connectionString = configuration.GetConnectionString("ServiceBus")
                ?? throw new InvalidOperationException(
                    "Configure either ServiceBus:Namespace (Azure, managed identity) or ConnectionStrings:ServiceBus (local emulator).");

            return new ServiceBusClient(connectionString);
        });

        services.AddSingleton<EmailQueue>();
        services.AddHostedService<EmailWorker>();
        services.AddHostedService<NotifySubscriptionWorker>();
        services.AddHostedService<AuditSubscriptionWorker>();

        return services;
    }
}
