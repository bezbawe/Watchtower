using Watchtower.Entities.Alerts;

namespace Watchtower.Alerting;

// Единый путь публикации алертов: сохранить в БД + разослать (live на дашборд + Telegram).
// Используется и живой L1-детекцией (DetectionAlertingHandler), и батчевой L2 (StatisticalDetectionJob).
public interface IAlertPublisher
{
    Task PublishAsync(IReadOnlyList<Alert> alerts, CancellationToken cancellationToken);
}
