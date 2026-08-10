using Watchtower.Entities.Alerts;

namespace Watchtower.Alerting;

// Отправка алерта в Telegram. Если канал не настроен — no-op.
public interface ITelegramAlertNotifier
{
    Task SendAsync(Alert alert, CancellationToken cancellationToken);
}
