using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Watchtower.Detection.Detectors;
using Watchtower.Detection.Statistics;

namespace Watchtower.Detection;

public static class WatchtowerDetectionRegistration
{
    public static IServiceCollection AddWatchtowerDetection(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DetectionOptions>(configuration.GetSection(DetectionOptions.SectionName));

        services.AddSingleton<IDetector>(sp => new BruteForceDetector(Opts(sp).BruteForce));
        services.AddSingleton<IDetector>(sp => new OffHoursDetector(Opts(sp).OffHours));
        services.AddSingleton<IDetector>(sp => new PrivilegeEscalationDetector(Opts(sp).PrivilegeEscalation));
        services.AddSingleton<IDetector>(sp => new ImpossibleTravelDetector(Opts(sp).ImpossibleTravel));

        services.AddSingleton<DetectionEngine>();

        // L2 — статистический детектор (не IDetector: работает не на батче, а на часовом ряде).
        services.AddSingleton(sp => new StatisticalAnomalyDetector(Opts(sp).Statistical));
        return services;
    }

    private static DetectionOptions Opts(IServiceProvider sp)
        => sp.GetRequiredService<IOptions<DetectionOptions>>().Value;
}
