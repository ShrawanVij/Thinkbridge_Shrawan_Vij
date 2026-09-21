using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuotesHub.Modules.Quotes.Application;

namespace QuotesHub.Modules.Quotes.Infrastructure;

// The single entry point the Host uses to wire this module in. Nothing
// outside this file needs to know Quotes is backed by SQLite/EF Core — the
// Host only ever calls AddQuotesModule.
public static class QuotesModule
{
    public static IServiceCollection AddQuotesModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<QuotesDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("Quotes") ?? "Data Source=quotes.db"));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<QuotesDbContext>());
        services.AddScoped<IQuoteRepository, QuoteRepository>();

        return services;
    }
}
