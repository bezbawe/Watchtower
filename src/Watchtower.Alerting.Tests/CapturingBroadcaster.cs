namespace Watchtower.Alerting.Tests;

// Тестовый IAlertBroadcaster: захватывает уведомления вместо реального SignalR-пуша.
internal sealed class CapturingBroadcaster : IAlertBroadcaster
{
    private readonly List<AlertNotification> _received = new();
    private readonly object _lock = new();

    public IReadOnlyList<AlertNotification> Received
    {
        get { lock (_lock) return _received.ToList(); }
    }

    public Task BroadcastAsync(AlertNotification notification, CancellationToken cancellationToken)
    {
        lock (_lock)
            _received.Add(notification);
        return Task.CompletedTask;
    }
}
