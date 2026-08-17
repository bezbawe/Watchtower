using Microsoft.Extensions.Logging;
using Watchtower.Detection;
using Watchtower.Entities.Events;
using Watchtower.Ingestion.Buffering;

namespace Watchtower.Alerting;

// Живой алертинг L1: прогоняет только что записанный батч событий через детекторы и публикует
// алерты (persist + live на дашборд + Telegram). Вызывается конвейером приёма после записи
// событий, когда у них уже проставлены Id (для RelatedEventIds).
public class DetectionAlertingHandler(
    DetectionEngine engine,
    IAlertPublisher publisher,
    ILogger<DetectionAlertingHandler> logger) : IIngestedBatchHandler
{
    public async Task HandleAsync(IReadOnlyList<LogEvent> batch, CancellationToken cancellationToken)
    {
        var alerts = engine.Run(batch);
        if (alerts.Count == 0)
            return;

        logger.LogInformation("L1 raised {Count} alert(s) from batch of {Batch} events", alerts.Count, batch.Count);
        await publisher.PublishAsync(alerts, cancellationToken);
    }
}
