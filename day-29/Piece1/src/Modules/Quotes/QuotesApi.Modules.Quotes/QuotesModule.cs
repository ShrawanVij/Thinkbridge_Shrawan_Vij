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
            var dbPath = OperatingSystem.IsWindows()
                ? Path.Combine(AppContext.BaseDirectory, "quotes.db")
                : "/tmp/quotes.db";

            options.UseSqlite(configuration.GetConnectionString("Quotes") ?? $"Data Source={dbPath}");
        });

        services.AddScoped<IQuoteRepository, QuoteRepository>();
        services.AddScoped<IQuoteOwnershipCheck, QuoteOwnershipCheck>();

        // TryAdd: Engagement also needs a ServiceBusClient (to consume, where
        // this publishes) — whichever module's AddXModule runs first wins,
        // the second call is a no-op rather than a second wasted client.
        services.TryAddSingleton(_ => new ServiceBusClient(
            configuration.GetConnectionString("ServiceBus")
                ?? throw new InvalidOperationException("ConnectionStrings:ServiceBus is not configured.")));
        services.AddSingleton<QuoteEventPublisher>();
        services.AddHostedService<OutboxRelay>();

        return services;
    }
}
