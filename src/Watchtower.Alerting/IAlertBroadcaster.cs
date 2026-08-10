namespace Watchtower.Alerting;

// Live-канал доставки алерта на дашборд. Реализация (SignalR) живёт в host-проекте.
public interface IAlertBroadcaster
{
    Task BroadcastAsync(AlertNotification notification, CancellationToken cancellationToken);
}
