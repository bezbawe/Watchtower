using Microsoft.AspNetCore.SignalR;
using Watchtower.Alerting;
using Watchtower.Web.Hubs;

namespace Watchtower.Web.Alerting;

// Host-реализация live-канала: пушит алерт всем подключённым дашбордам через SignalR.
public class SignalRAlertBroadcaster(IHubContext<AlertsHub> hub) : IAlertBroadcaster
{
    public const string AlertRaised = "AlertRaised";

    public Task BroadcastAsync(AlertNotification notification, CancellationToken cancellationToken)
        => hub.Clients.All.SendAsync(AlertRaised, notification, cancellationToken);
}
