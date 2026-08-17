using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Watchtower.Entities.Alerts;
using Watchtower.Entities.Enums;
using Watchtower.Repository.Interfaces;

namespace Watchtower.Alerting.Tests;

public class AlertPublisherTests
{
    private static Alert Alert(string detector) => new()
    {
        Severity = AlertSeverity.High,
        DetectorName = detector,
        Title = $"{detector} alert",
        Explanation = "because reasons",
    };

    [Fact]
    public async Task Publish_PersistsBatch_AndNotifiesEachAlert()
    {
        var repo = new Mock<IAlertRepository>();
        var broadcaster = new Mock<IAlertBroadcaster>();
        var telegram = new Mock<ITelegramAlertNotifier>();

        var publisher = new AlertPublisher(
            repo.Object, broadcaster.Object, telegram.Object, NullLogger<AlertPublisher>.Instance);

        var alerts = new[] { Alert("brute_force"), Alert("statistical_volume") };
        await publisher.PublishAsync(alerts, CancellationToken.None);

        repo.Verify(r => r.AddRangeAsync(It.Is<List<Alert>>(l => l.Count == 2)), Times.Once);
        broadcaster.Verify(b => b.BroadcastAsync(It.IsAny<AlertNotification>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        telegram.Verify(t => t.SendAsync(It.IsAny<Alert>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Publish_EmptyList_DoesNothing()
    {
        var repo = new Mock<IAlertRepository>();
        var broadcaster = new Mock<IAlertBroadcaster>();
        var telegram = new Mock<ITelegramAlertNotifier>();

        var publisher = new AlertPublisher(
            repo.Object, broadcaster.Object, telegram.Object, NullLogger<AlertPublisher>.Instance);

        await publisher.PublishAsync([], CancellationToken.None);

        repo.Verify(r => r.AddRangeAsync(It.IsAny<List<Alert>>()), Times.Never);
        broadcaster.Verify(b => b.BroadcastAsync(It.IsAny<AlertNotification>(), It.IsAny<CancellationToken>()), Times.Never);
        telegram.Verify(t => t.SendAsync(It.IsAny<Alert>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
