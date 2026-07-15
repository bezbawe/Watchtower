using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Watchtower.Repository;

public static class WatchtowerDbConnectionConfigFactory
{
    public static IServiceCollection AddWatchtowerDbContext(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<WatchtowerDbContext>(options => options.UseNpgsql(connectionString));
        services.AddDbContextFactory<WatchtowerDbContext>(options => options.UseNpgsql(connectionString), ServiceLifetime.Scoped);
        return services;
    }
}
