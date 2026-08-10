using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Watchtower.Ingestion.Buffering;

namespace Watchtower.Alerting;

public static class WatchtowerAlertingRegistration
{
    // Навешивает детекцию+алертинг на конвейер приёма (IIngestedBatchHandler) и Telegram-канал.
    // Требует уже зарегистрированной детекции (AddWatchtowerDetection) и репозиториев.
    // Реализацию IAlertBroadcaster (SignalR) регистрирует host — она завязана на IHubContext.
    public static IServiceCollection AddWatchtowerAlerting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TelegramOptions>(configuration.GetSection(TelegramOptions.SectionName));
        services.AddHttpClient<ITelegramAlertNotifier, TelegramAlertNotifier>();

        services.AddScoped<IIngestedBatchHandler, DetectionAlertingHandler>();
        return services;
    }
}
