using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuotesApi.Contracts;
using QuotesApi.Modules.Quotes.Data;
using QuotesApi.Modules.Quotes.Messaging;
using QuotesApi.Modules.Quotes.Outbox;
using QuotesApi.Modules.Quotes.Repositories;

namespace QuotesApi.Modules.Quotes;

// The single entry point the Host uses to wire this module in. Nothing
// outside this file needs to know Quotes is backed by SQLite, or that it
// publishes to Service Bus via an outbox relay.
public static class QuotesModule
{
    public static IServiceCollection AddQuotesModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<QuotesDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("Quotes");

            // Azure: a SQL Server connection string (AAD managed-identity
            // auth, no password) arrives via ConnectionStrings:Quotes. Local
            // dev: nothing provisions a real SQL Server, so fall back to a
            // SQLite file — same distinction each module makes.
            if (!string.IsNullOrEmpty(connectionString) && connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlServer(connectionString);
                return;
            }

            var dbPath = OperatingSystem.IsWindows()
                ? Path.Combine(AppContext.BaseDirectory, "quotes.db")
                : "/tmp/quotes.db";

            options.UseSqlite(connectionString ?? $"Data Source={dbPath}");
        });

        services.AddScoped<IQuoteRepository, QuoteRepository>();
        services.AddScoped<IQuoteOwnershipCheck, QuoteOwnershipCheck>();

        // TryAdd: Engagement also needs a ServiceBusClient (to consume, where
        // this publishes) — whichever module's AddXModule runs first wins,
        // the second call is a no-op rather than a second wasted client.
        // Azure: the namespace disables SAS keys entirely (disableLocalAuth),
        // so it's a host name + the app's own managed identity, not a
        // connection string. Local dev (the emulator) is connection-string
        // only, since it doesn't speak AAD at all.
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
        services.AddSingleton<QuoteEventPublisher>();
        services.AddHostedService<OutboxRelay>();

        return services;
    }
}
