using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Watchtower.Detection;
using Watchtower.Detection.Detectors;
using Watchtower.Entities.Alerts;
using Watchtower.Repository.Interfaces;

namespace Watchtower.Alerting.Tests;

public class DetectionAlertingHandlerTests
{
    private static DetectionEngine BruteForceEngine() =>
        new([new BruteForceDetector(new BruteForceOptions { Threshold = 5, WindowMinutes = 15 })]);

    [Fact]
    public async Task Handle_BruteForceBatch_PersistsAndNotifiesEachAlert()
    {
        var repo = new Mock<IAlertRepository>();
        var broadcaster = new Mock<IAlertBroadcaster>();
        var telegram = new Mock<ITelegramAlertNotifier>();

        var handler = new DetectionAlertingHandler(
            BruteForceEngine(), repo.Object, broadcaster.Object, telegram.Object,
            NullLogger<DetectionAlertingHandler>.Instance);

        await handler.HandleAsync(AlertingTestData.BruteForceBatch("203.0.113.9", count: 6), CancellationToken.None);

        repo.Verify(r => r.AddRangeAsync(
            It.Is<List<Alert>>(l => l.Count == 1 && l[0].DetectorName == "brute_force")), Times.Once);
        broadcaster.Verify(b => b.BroadcastAsync(
            It.Is<AlertNotification>(n => n.DetectorName == "brute_force"), It.IsAny<CancellationToken>()), Times.Once);
        telegram.Verify(t => t.SendAsync(It.IsAny<Alert>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoAnomalies_PersistsNothingAndDoesNotNotify()
    {
        var repo = new Mock<IAlertRepository>();
        var broadcaster = new Mock<IAlertBroadcaster>();
        var telegram = new Mock<ITelegramAlertNotifier>();

        var handler = new DetectionAlertingHandler(
            BruteForceEngine(), repo.Object, broadcaster.Object, telegram.Object,
            NullLogger<DetectionAlertingHandler>.Instance);

        // Одна неудача — ниже порога, алертов нет.
        await handler.HandleAsync(AlertingTestData.BruteForceBatch("203.0.113.9", count: 1), CancellationToken.None);

        repo.Verify(r => r.AddRangeAsync(It.IsAny<List<Alert>>()), Times.Never);
        broadcaster.Verify(b => b.BroadcastAsync(It.IsAny<AlertNotification>(), It.IsAny<CancellationToken>()), Times.Never);
        telegram.Verify(t => t.SendAsync(It.IsAny<Alert>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
