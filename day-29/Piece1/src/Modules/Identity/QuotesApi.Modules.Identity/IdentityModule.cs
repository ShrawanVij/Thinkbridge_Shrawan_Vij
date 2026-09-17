using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuotesApi.Modules.Identity.Authorization;
using QuotesApi.Modules.Identity.Data;

namespace QuotesApi.Modules.Identity;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<IdentityDbContext>(options =>
        {
            var dbPath = OperatingSystem.IsWindows()
                ? Path.Combine(AppContext.BaseDirectory, "identity.db")
                : "/tmp/identity.db";

            options.UseSqlite(configuration.GetConnectionString("Identity") ?? $"Data Source={dbPath}");
        });

        services.AddScoped<IAuthorizationHandler, CanModifyOwnQuoteHandler>();

        return services;
    }
}
