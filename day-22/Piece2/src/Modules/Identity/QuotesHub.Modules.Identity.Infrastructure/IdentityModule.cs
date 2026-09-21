using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuotesHub.Modules.Identity.Application;

namespace QuotesHub.Modules.Identity.Infrastructure;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IUserRepository, InMemoryUserRepository>();
        return services;
    }
}
