using Microsoft.Extensions.Logging;
using Watchtower.Entities.Alerts;
using Watchtower.Repository.Interfaces;

namespace Watchtower.Alerting;

public class AlertPublisher(
    IAlertRepository alertRepository,
    IAlertBroadcaster broadcaster,
    ITelegramAlertNotifier telegram,
    ILogger<AlertPublisher> logger) : IAlertPublisher
{
    public async Task PublishAsync(IReadOnlyList<Alert> alerts, CancellationToken cancellationToken)
    {
        if (alerts.Count == 0)
            return;

        await alertRepository.AddRangeAsync(alerts.ToList());
        logger.LogInformation("Published {Count} alert(s)", alerts.Count);

        foreach (var alert in alerts)
        {
            await broadcaster.BroadcastAsync(AlertNotification.FromAlert(alert), cancellationToken);
            await telegram.SendAsync(alert, cancellationToken);
        }
    }
}
