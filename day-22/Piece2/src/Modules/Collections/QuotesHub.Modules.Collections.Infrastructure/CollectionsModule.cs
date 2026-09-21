using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuotesHub.Modules.Collections.Application;

namespace QuotesHub.Modules.Collections.Infrastructure;

public static class CollectionsModule
{
    public static IServiceCollection AddCollectionsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ICollectionRepository, InMemoryCollectionRepository>();
        return services;
    }
}
