using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Watchtower.Detection;
using Watchtower.Detection.Detectors;
using Watchtower.Entities.Alerts;

namespace Watchtower.Alerting.Tests;

public class DetectionAlertingHandlerTests
{
    private static DetectionEngine BruteForceEngine() =>
        new([new BruteForceDetector(new BruteForceOptions { Threshold = 5, WindowMinutes = 15 })]);

    [Fact]
    public async Task Handle_BruteForceBatch_PublishesRaisedAlerts()
    {
        var publisher = new Mock<IAlertPublisher>();

        var handler = new DetectionAlertingHandler(
            BruteForceEngine(), publisher.Object, NullLogger<DetectionAlertingHandler>.Instance);

        await handler.HandleAsync(AlertingTestData.BruteForceBatch("203.0.113.9", count: 6), CancellationToken.None);

        publisher.Verify(p => p.PublishAsync(
            It.Is<IReadOnlyList<Alert>>(l => l.Count == 1 && l[0].DetectorName == "brute_force"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoAnomalies_DoesNotPublish()
    {
        var publisher = new Mock<IAlertPublisher>();

        var handler = new DetectionAlertingHandler(
            BruteForceEngine(), publisher.Object, NullLogger<DetectionAlertingHandler>.Instance);

        // Одна неудача — ниже порога, алертов нет.
        await handler.HandleAsync(AlertingTestData.BruteForceBatch("203.0.113.9", count: 1), CancellationToken.None);

        publisher.Verify(p => p.PublishAsync(It.IsAny<IReadOnlyList<Alert>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
