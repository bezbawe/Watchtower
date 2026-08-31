using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure;
using Watchtower.Alerting.Reporting;
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

        services.AddScoped<IAlertPublisher, AlertPublisher>();
        services.AddScoped<IIngestedBatchHandler, DetectionAlertingHandler>();

        // PDF-отчёты по инцидентам (QuestPDF Community — бесплатная лицензия для этого use case).
        QuestPDF.Settings.License = LicenseType.Community;
        services.AddSingleton<IncidentReportService>();

        // L2/L3 батчевая детекция; расписание (Hangfire) навешивает host.
        services.AddScoped<StatisticalDetectionJob>();
        services.AddScoped<SpikeDetectionJob>();
        return services;
    }
}
