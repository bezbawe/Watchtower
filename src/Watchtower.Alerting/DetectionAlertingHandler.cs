using Microsoft.Extensions.Logging;
using Watchtower.Detection;
using Watchtower.Entities.Events;
using Watchtower.Ingestion.Buffering;
using Watchtower.Repository.Interfaces;

namespace Watchtower.Alerting;

// Живой алертинг L1: прогоняет только что записанный батч событий через детекторы,
// сохраняет алерты и рассылает их (live на дашборд + Telegram). Вызывается конвейером
// приёма после записи событий, когда у них уже проставлены Id (для RelatedEventIds).
public class DetectionAlertingHandler(
    DetectionEngine engine,
    IAlertRepository alertRepository,
    IAlertBroadcaster broadcaster,
    ITelegramAlertNotifier telegram,
    ILogger<DetectionAlertingHandler> logger) : IIngestedBatchHandler
{
    public async Task HandleAsync(IReadOnlyList<LogEvent> batch, CancellationToken cancellationToken)
    {
        var alerts = engine.Run(batch);
        if (alerts.Count == 0)
            return;

        await alertRepository.AddRangeAsync(alerts.ToList());
        logger.LogInformation("Raised {Count} alert(s) from batch of {Batch} events", alerts.Count, batch.Count);

        foreach (var alert in alerts)
        {
            await broadcaster.BroadcastAsync(AlertNotification.FromAlert(alert), cancellationToken);
            await telegram.SendAsync(alert, cancellationToken);
        }
    }
}
