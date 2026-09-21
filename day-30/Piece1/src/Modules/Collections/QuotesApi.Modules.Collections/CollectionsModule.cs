using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuotesApi.Contracts;
using QuotesApi.Modules.Collections.Data;
using QuotesApi.Modules.Collections.Repositories;

namespace QuotesApi.Modules.Collections;

public static class CollectionsModule
{
    public static IServiceCollection AddCollectionsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CollectionsDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("Collections");

            if (!string.IsNullOrEmpty(connectionString) && connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlServer(connectionString);
                return;
            }

            var dbPath = OperatingSystem.IsWindows()
                ? Path.Combine(AppContext.BaseDirectory, "collections.db")
                : "/tmp/collections.db";

            options.UseSqlite(connectionString ?? $"Data Source={dbPath}");
        });

        services.AddScoped<ICollectionRepository, CollectionRepository>();
        services.AddSingleton<IClock, SystemClock>();

        return services;
    }
}
