using Microsoft.Extensions.DependencyInjection;
using Watchtower.Repository.Implementations;
using Watchtower.Repository.Interfaces;

namespace Watchtower.Repository;

public static class WatchtowerRepositoriesRegistration
{
    public static IServiceCollection AddWatchtowerRepositories(this IServiceCollection services)
    {
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IAlertRepository, AlertRepository>();
        return services;
    }
}
