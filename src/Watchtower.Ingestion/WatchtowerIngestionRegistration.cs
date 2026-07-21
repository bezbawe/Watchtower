using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Watchtower.Ingestion.Buffering;
using Watchtower.Ingestion.Enrichment;
using Watchtower.Ingestion.Normalization;
using Watchtower.Ingestion.Parsing;

namespace Watchtower.Ingestion;

public static class WatchtowerIngestionRegistration
{
    public static IServiceCollection AddWatchtowerIngestion(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<IngestionOptions>(configuration.GetSection(IngestionOptions.SectionName));

        services.AddSingleton<IGeoIpResolver, StubGeoIpResolver>();
        services.AddSingleton<ILogEventNormalizer, LogEventNormalizer>();
        services.AddSingleton<ITextLogParser, LogfmtParser>();

        services.AddSingleton<IEventIngestQueue, ChannelEventIngestQueue>();
        services.AddHostedService<EventIngestBackgroundService>();

        return services;
    }
}
